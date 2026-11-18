namespace AndyTours.Daten;

using System.Buffers;
using System.Security;
using System.Security.Cryptography;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten.Datensicherung;

/// <summary>
/// Resolves only previously catalogued sources. Callers must hold an outer dataset lease and independently
/// exclude external writers; FileShare.Read is not hostile Unix-writer protection or restore validation.
/// </summary>
public sealed partial class DateisystemSicherungskatalog
{
    public async Task<SicherungsdownloadQuelle> Oeffnen(
        SicherungsdownloadAnfrage anfrage, long serverMaxBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(anfrage);
        var source = await OeffnePruefquelle(
            new WiederherstellungsvorschauAnfrage(anfrage.SicherungsId, anfrage.ErwarteterInhaltHash),
            serverMaxBytes, cancellationToken).ConfigureAwait(false);
        return new SicherungsdownloadQuelle(source.Inhalt, source.Metadaten, source.DisposeAsync);
    }

    private const string QuelleNichtGefunden = "Sicherungsquelle nicht gefunden.";
    private const string QuelleGeaendert = "Sicherungsquelle wurde geändert.";

    public async Task<SicherungsquellenLease> OeffnePruefquelle(
        WiederherstellungsvorschauAnfrage anfrage,
        long serverMaxBytes,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var geprueft = WiederherstellungsvorschauRegeln.PruefeAnfrage(anfrage);
        if (serverMaxBytes <= 0) throw UnavailableSource();
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        FileStream? ownedStream = null;
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_quellen.TryGetValue(geprueft.SicherungsId, out var source)) throw MissingSource();
            if (!WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig(source.Metadaten))
                throw UnavailableSource();
            if (!string.Equals(source.Metadaten.InhaltHash, geprueft.ErwarteterInhaltHash, StringComparison.OrdinalIgnoreCase))
                throw ChangedSource();
            if (source.Metadaten.Byteanzahl < 0 || source.Metadaten.Byteanzahl > serverMaxBytes)
                throw UnavailableSource();

            var path = Path.GetFullPath(source.AbsoluterPfad);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(Path.GetDirectoryName(path), _ordner, comparison))
                throw UnavailableSource();
            FileAttributes attributes;
            try { attributes = File.GetAttributes(path); }
            catch (FileNotFoundException) { throw MissingSource(); }
            catch (DirectoryNotFoundException) { throw MissingSource(); }
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
                HasReparseAncestorForSource(path))
                throw UnavailableSource();

            var before = new FileInfo(path);
            if (!before.Exists) throw MissingSource();
            var initialLength = before.Length;
            var initialWrite = before.LastWriteTimeUtc;
            try
            {
                ownedStream = new FileStream(path, new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.Read,
                    BufferSize = 81920,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan
                });
            }
            catch (FileNotFoundException) { throw MissingSource(); }
            catch (DirectoryNotFoundException) { throw MissingSource(); }
            catch (UnauthorizedAccessException) { throw UnavailableSource(); }

            if (ownedStream.Length != source.Metadaten.Byteanzahl || ownedStream.Length > serverMaxBytes)
                throw ChangedSource();

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                long total = 0;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var read = await ownedStream.ReadAsync(buffer.AsMemory(0, 81920), cancellationToken).ConfigureAwait(false);
                    if (read == 0) break;
                    total = checked(total + read);
                    if (total > source.Metadaten.Byteanzahl || total > serverMaxBytes) throw ChangedSource();
                    hash.AppendData(buffer, 0, read);
                }
                if (total != source.Metadaten.Byteanzahl || ownedStream.Length != total)
                    throw ChangedSource();
            }
            finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }

            cancellationToken.ThrowIfCancellationRequested();
            FileAttributes finalAttributes;
            try { finalAttributes = File.GetAttributes(path); }
            catch (FileNotFoundException) { throw MissingSource(); }
            catch (DirectoryNotFoundException) { throw MissingSource(); }
            if ((finalAttributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw UnavailableSource();
            var after = new FileInfo(path);
            after.Refresh();
            if (HasReparseAncestorForSource(path)) throw UnavailableSource();
            if (!after.Exists) throw MissingSource();
            if (after.Length != initialLength || after.LastWriteTimeUtc != initialWrite ||
                initialLength != source.Metadaten.Byteanzahl || ownedStream.Length != initialLength)
                throw ChangedSource();

            var actualHash = Convert.ToHexString(hash.GetHashAndReset());
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(source.Metadaten.InhaltHash), Convert.FromHexString(actualHash)))
                throw ChangedSource();

            ownedStream.Position = 0;
            cancellationToken.ThrowIfCancellationRequested();
            var lease = new SicherungsquellenLease(ownedStream, source.Metadaten);
            ownedStream = null;
            return lease;
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw; }
        catch (SicherungskatalogNichtVerfuegbarException) { throw; }
        catch (FileNotFoundException) { throw MissingSource(); }
        catch (DirectoryNotFoundException) { throw MissingSource(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or
                                        ArgumentException or NotSupportedException or OverflowException or CryptographicException)
        {
            throw UnavailableSource();
        }
        finally
        {
            try
            {
                if (ownedStream is not null)
                {
                    try { await ownedStream.DisposeAsync().ConfigureAwait(false); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (SecurityException) { }
                    catch (ObjectDisposedException) { }
                }
            }
            finally { _gate.Release(); }
        }
    }

    private static bool HasReparseAncestorForSource(string path)
    {
        for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current);)
        {
            var attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) != 0) return true;
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, current,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) break;
            current = parent;
        }
        return false;
    }

    private static StammdatenException MissingSource() =>
        new(StammdatenFehlerart.NichtGefunden, QuelleNichtGefunden);

    private static StammdatenException ChangedSource() =>
        new(StammdatenFehlerart.Konflikt, QuelleGeaendert);

    private static SicherungskatalogNichtVerfuegbarException UnavailableSource() => new();
}
