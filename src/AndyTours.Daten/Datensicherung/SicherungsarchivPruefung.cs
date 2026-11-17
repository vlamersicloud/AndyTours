namespace AndyTours.Daten.Datensicherung;

using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

/// <summary>Read-only verifier for strict manifest JSON v1. Caller retains ownership and must keep the seekable input unchanged through preflight and parsing. No SQLite, snapshot, or restore validity is implied.</summary>
public static class SicherungsarchivPruefung
{
    private const string Fehlertext = "Sicherungsarchiv ungültig.";
    private const string ManifestName = "manifest.json";
    private const int BufferSize = 81920;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        RespectRequiredConstructorParameters = true,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public static async Task<Sicherungsmanifest> Pruefe(
        Stream archiv,
        SicherungsmanifestGrenzen grenzen,
        IReadOnlyList<string> ausgeschlosseneRelativePfade,
        long maxArchivBytes,
        long maxManifestBytes,
        long maxZentralverzeichnisBytes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (archiv is null || grenzen is null || ausgeschlosseneRelativePfade is null ||
                !archiv.CanRead || !archiv.CanSeek || maxArchivBytes <= 0 || maxManifestBytes <= 0 ||
                maxManifestBytes > Array.MaxLength || maxZentralverzeichnisBytes <= 0 ||
                grenzen.MaxEintraege <= 0 || grenzen.MaxDateiBytes <= 0 || grenzen.MaxGesamtBytes <= 0 ||
                grenzen.MaxPfadzeichen <= 0)
                throw Invalid();

            var struktur = await SicherungsZipVorpruefung.Pruefe(
                archiv, checked(grenzen.MaxEintraege + 1), maxArchivBytes, maxZentralverzeichnisBytes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            archiv.Position = 0;
            using var zip = new ZipArchive(archiv, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Count != struktur.Eintraege || zip.Entries.Count > grenzen.MaxEintraege + 1)
                throw Invalid();

            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            ZipArchiveEntry? manifestEntry = null;
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(entry.FullName) || !entries.TryAdd(entry.FullName, entry) ||
                    entry.FullName.EndsWith('/') || IsDirectoryOrLink(entry))
                    throw Invalid();
                if (string.Equals(entry.FullName, ManifestName, StringComparison.Ordinal))
                    manifestEntry = entry;
                else if (string.Equals(entry.FullName, ManifestName, StringComparison.OrdinalIgnoreCase))
                    throw Invalid();
            }
            if (manifestEntry is null || manifestEntry.Length < 0 || manifestEntry.Length > maxManifestBytes)
                throw Invalid();

            byte[] manifestBytes;
            await using (var manifestStream = manifestEntry.Open())
            using (var buffer = new MemoryStream())
            {
                var rented = ArrayPool<byte>.Shared.Rent(BufferSize);
                try
                {
                    long readTotal = 0;
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var read = await manifestStream.ReadAsync(rented.AsMemory(0, BufferSize), cancellationToken);
                        if (read == 0) break;
                        readTotal = checked(readTotal + read);
                        if (readTotal > maxManifestBytes || readTotal > manifestEntry.Length) throw Invalid();
                        buffer.Write(rented, 0, read);
                    }
                    if (readTotal != manifestEntry.Length) throw Invalid();
                    manifestBytes = buffer.ToArray();
                }
                finally { ArrayPool<byte>.Shared.Return(rented, clearArray: true); }
            }

            Sicherungsmanifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<Sicherungsmanifest>(manifestBytes, JsonOptions) ?? throw Invalid();
            }
            catch (JsonException) { throw Invalid(); }
            var validated = SicherungsmanifestRegeln.Pruefe(manifest, grenzen, ausgeschlosseneRelativePfade, cancellationToken);
            var expectedZipEntries = checked(validated.Eintraege.Count + 1);
            if (entries.Count != expectedZipEntries) throw Invalid();

            var bufferBytes = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                long totalBytes = 0;
                foreach (var declared in validated.Eintraege)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!entries.TryGetValue(declared.RelativerPfad, out var payload) ||
                        !string.Equals(payload.FullName, declared.RelativerPfad, StringComparison.Ordinal) ||
                        payload.Length < 0 || payload.Length != declared.Byteanzahl ||
                        payload.Length > grenzen.MaxDateiBytes)
                        throw Invalid();

                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    await using var payloadStream = payload.Open();
                    long fileBytes = 0;
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var read = await payloadStream.ReadAsync(bufferBytes.AsMemory(0, BufferSize), cancellationToken);
                        if (read == 0) break;
                        fileBytes = checked(fileBytes + read);
                        totalBytes = checked(totalBytes + read);
                        if (fileBytes > declared.Byteanzahl || fileBytes > grenzen.MaxDateiBytes ||
                            totalBytes > grenzen.MaxGesamtBytes) throw Invalid();
                        hash.AppendData(bufferBytes, 0, read);
                    }
                    if (fileBytes != declared.Byteanzahl) throw Invalid();
                    var expectedHash = Convert.FromHexString(declared.InhaltHash);
                    var actualHash = hash.GetHashAndReset();
                    if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash)) throw Invalid();
                }
            }
            finally { ArrayPool<byte>.Shared.Return(bufferBytes, clearArray: true); }

            cancellationToken.ThrowIfCancellationRequested();
            return validated;
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw Invalid(); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or
                                        ArgumentException or NotSupportedException or OverflowException or
                                        ObjectDisposedException or CryptographicException)
        { throw Invalid(); }
    }

    private static bool IsDirectoryOrLink(ZipArchiveEntry entry)
    {
        var attributes = entry.ExternalAttributes;
        var dosAttributes = (FileAttributes)(attributes & 0xffff);
        if ((dosAttributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) return true;
        var unixType = (attributes >> 16) & 0xF000;
        return unixType != 0 && unixType != 0x8000;
    }

    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, Fehlertext);
}
