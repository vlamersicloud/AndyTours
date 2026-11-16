using System.Net.Http.Json;
using AndyTours.Application.Planung;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

public sealed partial class RoutingKonfigurationHttpClient : IRoutingKonfiguration
{
    public Task<RoutingKonfigurationAntwort> AdresseZuruecksetzen(RoutingVersionAnfrage anfrage, CancellationToken cancellationToken)
    {
        ValidateVersionRequest(anfrage);
        return Send(JsonRequest(HttpMethod.Post, "api/v1/routing-konfiguration/brouter-adresse/zuruecksetzen", anfrage), cancellationToken);
    }

    public Task<RoutingKonfigurationAntwort> OrsKeySpeichern(RoutingOrsSchluesselAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null) throw Invalid();
        RoutingKonfigurationRegeln.PruefeVersion(anfrage.Version);
        _ = RoutingKonfigurationRegeln.OrsSchluessel(anfrage.Schluessel);
        return Send(JsonRequest(HttpMethod.Put, "api/v1/routing-konfiguration/ors-schluessel", anfrage), cancellationToken);
    }

    public Task<RoutingKonfigurationAntwort> OrsKeyEntfernen(RoutingVersionAnfrage anfrage, CancellationToken cancellationToken)
    {
        ValidateVersionRequest(anfrage);
        return Send(JsonRequest(HttpMethod.Post, "api/v1/routing-konfiguration/ors-schluessel/entfernen", anfrage), cancellationToken);
    }

    private static void ValidateVersionRequest(RoutingVersionAnfrage anfrage)
    {
        if (anfrage is null) throw Invalid();
        RoutingKonfigurationRegeln.PruefeVersion(anfrage.Version);
    }
}
