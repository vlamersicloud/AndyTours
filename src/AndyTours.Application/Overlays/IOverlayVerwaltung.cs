using AndyTours.Contracts.V1;

namespace AndyTours.Application.Overlays;

/// <summary>
/// Finite overlay mutation operations. Every operation compares the aggregate snapshot
/// version; writes are targeted mutations, never full-snapshot replacement.
/// No SQL, filesystem path, or GPX payload crosses this port.
/// </summary>
public interface IOverlayVerwaltung
{
    Task<OverlayAnlegenAntwort> OrdnerAnlegen(OverlayOrdnerAnlegenAnfrage anfrage, CancellationToken cancellationToken);
    Task<OverlayBestandAntwort> OrdnerAendern(long ordnerId, OverlayOrdnerAendernAnfrage anfrage, CancellationToken cancellationToken);
    Task<OverlayBestandAntwort> OrdnerLoeschen(long ordnerId, string version, CancellationToken cancellationToken);
    Task<OverlayBestandAntwort> OverlayAendern(long overlayId, OverlayAendernAnfrage anfrage, CancellationToken cancellationToken);
    Task<OverlayBestandAntwort> OverlayLoeschen(long overlayId, string version, CancellationToken cancellationToken);
    Task<OverlayBestandAntwort> Sichtbarkeit(OverlaySichtbarkeitAnfrage anfrage, CancellationToken cancellationToken);
    Task<OverlayBestandAntwort> GemeinsameFarbe(OverlayGemeinsameFarbeAnfrage anfrage, CancellationToken cancellationToken);
}
