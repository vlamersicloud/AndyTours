namespace AndyTours.Daten.Datensicherung;

using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

/// <summary>Copies only the manifest-declared database payload to a caller-owned empty staging stream.</summary>
/// <remarks>
/// Both streams remain caller-owned and must be independent. The caller must hold an outer lease that
/// excludes external writers and provide a private, exclusively owned CreateNew destination bounded by
/// deployment policy; never use the active database as destination. Failure may leave partial output;
/// the caller must discard only its own destination. The result is not SQLite or restore validation.
/// </remarks>
public static class SicherungsdatenbankExtraktion
{
    private const string Fehlertext = "Sicherungsarchiv ungültig.";
    private const int BufferSize = 81920;

    public static async Task<Sicherungsmanifest> Kopiere(
        Stream archiv,
        Stream ziel,
        SicherungsarchivGrenzen grenzen,
        IReadOnlyList<string> ausgeschlosseneRelativePfade,
        string erwarteterArchivHash,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (archiv is null || ziel is null || grenzen is null || ausgeschlosseneRelativePfade is null ||
                ReferenceEquals(archiv, ziel) || !archiv.CanRead || !archiv.CanSeek || !ziel.CanWrite || !ziel.CanSeek ||
                ziel.Length != 0 || ziel.Position != 0 || grenzen.Manifest is null ||
                grenzen.MaxArchivBytes <= 0 || grenzen.MaxManifestBytes <= 0 || grenzen.MaxZentralverzeichnisBytes <= 0 ||
                grenzen.Manifest.MaxEintraege <= 0 || grenzen.Manifest.MaxDateiBytes <= 0 ||
                grenzen.Manifest.MaxGesamtBytes <= 0 || grenzen.Manifest.MaxPfadzeichen <= 0 ||
                erwarteterArchivHash is not { Length: 64 } || !erwarteterArchivHash.All(Uri.IsHexDigit))
                throw Invalid();

            var originalLength = archiv.Length;
            if (originalLength <= 0 || originalLength > grenzen.MaxArchivBytes) throw Invalid();
            var actualArchiveHash = await HashWholeStream(archiv, originalLength, grenzen.MaxArchivBytes, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(erwarteterArchivHash), actualArchiveHash))
                throw Invalid();

            archiv.Position = 0;
            var manifest = await SicherungsarchivPruefung.Pruefe(
                archiv, grenzen.Manifest, ausgeschlosseneRelativePfade,
                grenzen.MaxArchivBytes, grenzen.MaxManifestBytes,
                grenzen.MaxZentralverzeichnisBytes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var dbEntries = manifest.Eintraege.Where(e => e.Art == SicherungsmanifestEintragart.Datenbank &&
                string.Equals(e.RelativerPfad, "andytours.db", StringComparison.Ordinal)).ToArray();
            if (dbEntries.Length != 1) throw Invalid();
            var database = dbEntries[0];

            archiv.Position = 0;
            using (var zip = new ZipArchive(archiv, ZipArchiveMode.Read, leaveOpen: true))
            {
                var matches = zip.Entries.Where(e => string.Equals(e.FullName, database.RelativerPfad, StringComparison.Ordinal)).ToArray();
                if (matches.Length != 1 || matches[0].Length != database.Byteanzahl) throw Invalid();
                await using var source = matches[0].Open();
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var rented = ArrayPool<byte>.Shared.Rent(BufferSize);
                try
                {
                    long copied = 0;
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var read = await source.ReadAsync(rented.AsMemory(0, BufferSize), cancellationToken);
                        if (read == 0) break;
                        copied = checked(copied + read);
                        if (copied > database.Byteanzahl || copied > grenzen.Manifest.MaxDateiBytes ||
                            copied > grenzen.Manifest.MaxGesamtBytes) throw Invalid();
                        await ziel.WriteAsync(rented.AsMemory(0, read), cancellationToken);
                        hash.AppendData(rented, 0, read);
                    }
                    if (copied != database.Byteanzahl || ziel.Length != database.Byteanzahl) throw Invalid();
                    var expectedDbHash = Convert.FromHexString(database.InhaltHash);
                    if (!CryptographicOperations.FixedTimeEquals(expectedDbHash, hash.GetHashAndReset())) throw Invalid();
                }
                finally { ArrayPool<byte>.Shared.Return(rented, clearArray: true); }
            }

            await ziel.FlushAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (archiv.Length != originalLength) throw Invalid();
            var finalHash = await HashWholeStream(archiv, originalLength, grenzen.MaxArchivBytes, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(erwarteterArchivHash), finalHash) ||
                archiv.Length != originalLength || ziel.Length != database.Byteanzahl)
                throw Invalid();
            cancellationToken.ThrowIfCancellationRequested();
            return manifest;
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw Invalid(); }
        catch (Exception ex) when (ex is InvalidDataException or OverflowException or CryptographicException or ArgumentException)
        { throw Invalid(); }
    }

    private static async Task<byte[]> HashWholeStream(Stream stream, long expectedLength, long maximumLength, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (expectedLength < 0 || expectedLength > maximumLength || stream.Length != expectedLength) throw Invalid();
        stream.Position = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var rented = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            long readTotal = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await stream.ReadAsync(rented.AsMemory(0, BufferSize), cancellationToken);
                if (read == 0) break;
                readTotal = checked(readTotal + read);
                if (readTotal > expectedLength || readTotal > maximumLength) throw Invalid();
                hash.AppendData(rented, 0, read);
            }
            if (readTotal != expectedLength || stream.Length != expectedLength) throw Invalid();
            cancellationToken.ThrowIfCancellationRequested();
            return hash.GetHashAndReset();
        }
        finally { ArrayPool<byte>.Shared.Return(rented, clearArray: true); }
    }

    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, Fehlertext);
}
