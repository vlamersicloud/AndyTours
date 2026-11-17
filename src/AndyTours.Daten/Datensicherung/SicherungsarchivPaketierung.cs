namespace AndyTours.Daten.Datensicherung;

using System.Buffers;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

/// <summary>Creates a bounded ZIP on a caller-owned stream. Caller holds the outer exclusive lease, supplies a private empty destination and same-root snapshot, keeps inputs immutable, and fully inspects before publication. This component does not publish or own the destination.</summary>
public static class SicherungsarchivPaketierung
{
    private const string Fehlertext = "Sicherungsarchiv konnte nicht erstellt werden.";
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

    public static async Task<Sicherungsmanifest> Erstellen(
        Stream ziel, string datenordner, SicherungsdatenbankSnapshot snapshot,
        IReadOnlyList<SicherungsQuelldatei> dateien, DateTimeOffset erstelltUtc,
        SicherungsmanifestGrenzen grenzen, IReadOnlyList<string> ausgeschlosseneRelativePfade,
        long maxArchivBytes, long maxManifestBytes, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ziel is null || snapshot is null || dateien is null || grenzen is null || ausgeschlosseneRelativePfade is null ||
                string.IsNullOrWhiteSpace(datenordner) || !Path.IsPathFullyQualified(datenordner) ||
                !ziel.CanWrite || !ziel.CanSeek || ziel.Length != 0 || ziel.Position != 0 ||
                erstelltUtc.Offset != TimeSpan.Zero || grenzen.MaxEintraege <= 0 || grenzen.MaxDateiBytes <= 0 ||
                grenzen.MaxGesamtBytes <= 0 || grenzen.MaxPfadzeichen <= 0 || maxArchivBytes <= 0 ||
                maxManifestBytes <= 0 || maxManifestBytes > Array.MaxLength ||
                (long)dateien.Count + 1 > grenzen.MaxEintraege)
                throw Invalid();

            var sources = dateien.ToArray();
            if ((long)sources.Length + 1 > grenzen.MaxEintraege) throw Invalid();
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(datenordner));
            if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.Directory) == 0 || HasReparseAncestor(root)) throw Invalid();
            var snapshotPath = Path.GetFullPath(snapshot.SnapshotPfad);
            var databaseInfo = new FileInfo(snapshotPath);
            if (!databaseInfo.Exists || databaseInfo.Length <= 0 || databaseInfo.Length > grenzen.MaxDateiBytes)
                throw Invalid();

            var candidates = new List<SicherungsmanifestEintrag>(sources.Length + 1)
            {
                new("andytours.db", SicherungsmanifestEintragart.Datenbank, databaseInfo.Length, new string('0', 64))
            };
            var paths = new HashSet<string>(StringComparer.Ordinal);
            long declaredTotal = databaseInfo.Length;
            foreach (var file in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (file is null || string.IsNullOrWhiteSpace(file.RelativerPfad) ||
                    !Path.IsPathFullyQualified(file.VollstaendigerPfad) || file.Byteanzahl < 0 ||
                    file.Byteanzahl > grenzen.MaxDateiBytes || !paths.Add(file.RelativerPfad)) throw Invalid();
                var full = Path.GetFullPath(file.VollstaendigerPfad);
                var relative = Path.GetRelativePath(root, full).Replace(Path.DirectorySeparatorChar, '/');
                if (!IsWithin(root, full) || !string.Equals(relative, file.RelativerPfad, StringComparison.Ordinal) ||
                    HasReparseAncestor(full) || SamePath(full, snapshotPath)) throw Invalid();
                declaredTotal = checked(declaredTotal + file.Byteanzahl);
                if (declaredTotal > grenzen.MaxGesamtBytes) throw Invalid();
                candidates.Add(new SicherungsmanifestEintrag(file.RelativerPfad, SicherungsmanifestEintragart.Datei,
                    file.Byteanzahl, new string('0', 64)));
            }
            var references = new HashSet<string>(snapshot.ReferenzierteRelativePfade, StringComparer.Ordinal);
            if (references.Any(reference => !paths.Contains(reference))) throw Invalid();
            _ = SicherungsmanifestRegeln.Pruefe(new Sicherungsmanifest(1, erstelltUtc, candidates), grenzen,
                ausgeschlosseneRelativePfade, cancellationToken);
            snapshot.PruefeUnveraendert(cancellationToken);

            var hashes = new List<SicherungsmanifestEintrag>(candidates.Count);
            using (var bounded = new BegrenzterSicherungsschreibStream(ziel, maxArchivBytes))
            using (var archive = new ZipArchive(bounded, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: System.Text.Encoding.UTF8))
            {
                var dbInfoNow = new FileInfo(snapshotPath);
                if (!SameMetadata(databaseInfo, dbInfoNow)) throw Conflict();
                var dbEntry = archive.CreateEntry("andytours.db", CompressionLevel.Fastest);
                dbEntry.ExternalAttributes = unchecked((int)0x81800000);
                var dbHash = await CopyAndHashAsync(snapshotPath, dbEntry, databaseInfo.Length, databaseInfo.LastWriteTimeUtc,
                    grenzen, cancellationToken);
                hashes.Add(new SicherungsmanifestEintrag("andytours.db", SicherungsmanifestEintragart.Datenbank, dbHash.Bytes, dbHash.Hash));

                foreach (var file in sources)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var full = Path.GetFullPath(file.VollstaendigerPfad);
                    var before = new FileInfo(full);
                    if (!before.Exists || before.Length != file.Byteanzahl || before.LastWriteTimeUtc != file.LetzteAenderungUtc || HasReparseAncestor(full))
                        throw Conflict();
                    var entry = archive.CreateEntry(file.RelativerPfad, CompressionLevel.Fastest);
                    entry.ExternalAttributes = unchecked((int)0x81800000);
                    var copied = await CopyAndHashAsync(full, entry, file.Byteanzahl, file.LetzteAenderungUtc,
                        grenzen, cancellationToken);
                    hashes.Add(new SicherungsmanifestEintrag(file.RelativerPfad, SicherungsmanifestEintragart.Datei, copied.Bytes, copied.Hash));
                }

                var manifest = SicherungsmanifestRegeln.Pruefe(new Sicherungsmanifest(1, erstelltUtc, hashes),
                    grenzen, ausgeschlosseneRelativePfade, cancellationToken);
                using var manifestBuffer = new MemoryStream();
                using (var manifestBounded = new BegrenzterSicherungsschreibStream(manifestBuffer, maxManifestBytes))
                    await JsonSerializer.SerializeAsync(manifestBounded, manifest, JsonOptions, cancellationToken);
                manifestBuffer.Position = 0;
                var manifestEntry = archive.CreateEntry(ManifestName, CompressionLevel.Fastest);
                manifestEntry.ExternalAttributes = unchecked((int)0x81800000);
                await using (var output = manifestEntry.Open())
                    await manifestBuffer.CopyToAsync(output, BufferSize, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            snapshot.PruefeUnveraendert(cancellationToken);
            return SicherungsmanifestRegeln.Pruefe(new Sicherungsmanifest(1, erstelltUtc, hashes),
                grenzen, ausgeschlosseneRelativePfade, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException ex) when (ex.Art == StammdatenFehlerart.Konflikt) { throw; }
        catch (StammdatenException) { throw Invalid(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                                        OverflowException or CryptographicException or SecurityException or
                                        NotSupportedException or ObjectDisposedException)
        { throw Invalid(); }
    }

    private static async Task<(long Bytes, string Hash)> CopyAndHashAsync(string path, ZipArchiveEntry entry,
        long expectedBytes, DateTime expectedUtc, SicherungsmanifestGrenzen limits, CancellationToken ct)
    {
        var before = new FileInfo(path);
        if (!before.Exists || before.Length != expectedBytes || before.LastWriteTimeUtc != expectedUtc || HasReparseAncestor(path)) throw Conflict();
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = entry.Open();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            long total = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var read = await input.ReadAsync(buffer.AsMemory(0, BufferSize), ct);
                if (read == 0) break;
                total = checked(total + read);
                if (total > expectedBytes || total > limits.MaxDateiBytes) throw Conflict();
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            if (total != expectedBytes) throw Conflict();
            var after = new FileInfo(path);
            if (!after.Exists || after.Length != expectedBytes || after.LastWriteTimeUtc != expectedUtc || HasReparseAncestor(path)) throw Conflict();
            return (total, Convert.ToHexString(hash.GetHashAndReset()));
        }
        finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }

    private static bool SameMetadata(FileInfo a, FileInfo b) => b.Exists && a.Length == b.Length && a.LastWriteTimeUtc == b.LastWriteTimeUtc;
    private static bool SamePath(string a, string b) => string.Equals(a, b, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static bool IsWithin(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative != "." && relative != ".." && !Path.IsPathRooted(relative) &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
    private static bool HasReparseAncestor(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true; }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || SamePath(parent, current)) break;
            current = parent;
        }
        return false;
    }
    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, Fehlertext);
    private static StammdatenException Conflict() => new(StammdatenFehlerart.Konflikt, "Datenbestand wurde während der Sicherung geändert.");
}
