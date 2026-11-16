using AndyTours.Contracts.V1;

namespace AndyTours.Application.Overlays;

/// <summary>
/// Imports one streamed GPX file as an overlay. The caller retains ownership of the input stream;
/// implementations enforce a 128 MiB per-file limit without a batch-file-count limit.
/// </summary>
public interface IOverlayGpxImport
{
    Task<OverlayAnlegenAntwort> Importieren(
        OverlayGpxImportAnfrage anfrage,
        Stream inhalt,
        CancellationToken cancellationToken);
}
