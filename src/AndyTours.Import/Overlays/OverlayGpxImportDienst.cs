using System.Security.Cryptography;
using System.Xml;
using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;

namespace AndyTours.Import.Overlays;

/// <summary>Secure bounded server-side streaming GPX importer using the shared Core parser.</summary>
public sealed class OverlayGpxImportDienst(IOverlayGpxSpeicher speicher) : IOverlayGpxImport
{
    public async Task<OverlayAnlegenAntwort> Importieren(
        OverlayGpxImportAnfrage anfrage, Stream inhalt, CancellationToken cancellationToken)
    {
        var normalized = OverlayGpxRegeln.Normalisieren(anfrage);
        if (inhalt is null || !inhalt.CanRead) throw Invalid();
        cancellationToken.ThrowIfCancellationRequested();

        using var buffer = new MemoryStream();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] chunk = new byte[64 * 1024];
        int read;
        try
        {
            while ((read = await inhalt.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (buffer.Length + read > OverlayGpxRegeln.MaxDateiBytes)
                    throw new OverlayGpxZuGrossException();
                hash.AppendData(chunk, 0, read);
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OverlayGpxZuGrossException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException or OperationCanceledException)
        {
            throw Invalid();
        }
        if (buffer.Length == 0) throw Invalid();

        string physicalHash = Convert.ToHexString(hash.GetHashAndReset());
        var receipt = await speicher.Quittung(normalized, physicalHash, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (receipt is not null) return receipt;

        buffer.Position = 0;
        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = OverlayGpxRegeln.MaxDateiBytes,
            CloseInput = false
        };
        IReadOnlyList<IReadOnlyList<AndyTours.Core.Modell.Geoposition>>? sections;
        try
        {
            using var reader = XmlReader.Create(buffer, settings);
            sections = await LeseGpxStrecke.AusfuehrenAsync(reader, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException or ArgumentException)
        {
            throw Invalid();
        }

        if (sections is null || sections.Count == 0) throw Invalid();
        cancellationToken.ThrowIfCancellationRequested();
        return await speicher.Importieren(
            new OverlayGpxVorbereitung(normalized, physicalHash, sections), cancellationToken).ConfigureAwait(false);
    }

    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, "Overlay-GPX-Datei ungültig.");
}
