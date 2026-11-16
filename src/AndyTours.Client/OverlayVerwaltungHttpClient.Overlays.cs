using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

public sealed partial class OverlayVerwaltungHttpClient : IOverlayVerwaltung
{
    private const string OverlayRoot = "api/v1/overlays/";

    public Task<OverlayBestandAntwort> OverlayAendern(long overlayId, OverlayAendernAnfrage anfrage, CancellationToken cancellationToken)
    {
        ValidiereId(overlayId);
        ArgumentNullException.ThrowIfNull(anfrage);
        ValidiereVersion(anfrage.Version);
        if (anfrage.OrdnerId is <= 0) throw Invalid();
        ValidiereName(anfrage.Name);
        if (anfrage.Farbe is { } color && !Enum.IsDefined(color)) throw Invalid();
        return Send<OverlayBestandAntwort>(HttpMethod.Put, OverlayRoot + Id(overlayId), anfrage, null, cancellationToken);
    }

    public Task<OverlayBestandAntwort> OverlayLoeschen(long overlayId, string version, CancellationToken cancellationToken)
    {
        ValidiereId(overlayId);
        ValidiereVersion(version);
        return Send<OverlayBestandAntwort>(HttpMethod.Delete, OverlayRoot + Id(overlayId), null, version, cancellationToken);
    }

    public Task<OverlayBestandAntwort> Sichtbarkeit(OverlaySichtbarkeitAnfrage anfrage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        ValidiereVersion(anfrage.Version);
        if (anfrage.OverlayIds is null || anfrage.OverlayIds.Count == 0 ||
            anfrage.OverlayIds.Any(id => id <= 0) || anfrage.OverlayIds.Distinct().Count() != anfrage.OverlayIds.Count)
            throw Invalid();
        return Send<OverlayBestandAntwort>(HttpMethod.Put, OverlayRoot + "sichtbarkeit", anfrage, null, cancellationToken);
    }

    public Task<OverlayBestandAntwort> GemeinsameFarbe(OverlayGemeinsameFarbeAnfrage anfrage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        ValidiereVersion(anfrage.Version);
        if (!Enum.IsDefined(anfrage.Farbe)) throw Invalid();
        return Send<OverlayBestandAntwort>(HttpMethod.Put, OverlayRoot + "farbe", anfrage, null, cancellationToken);
    }
}
