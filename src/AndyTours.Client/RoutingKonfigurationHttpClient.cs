using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

/// <summary>Typed HTTP operations for safe routing configuration. Uses the caller-owned HttpClient.</summary>
public sealed partial class RoutingKonfigurationHttpClient(HttpClient http)
{
    public Task<RoutingKonfigurationAntwort> Laden(CancellationToken cancellationToken) =>
        Send(new HttpRequestMessage(HttpMethod.Get, "api/v1/routing-konfiguration"), cancellationToken);

    public Task<RoutingKonfigurationAntwort> DienstSpeichern(RoutingDienstAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null) throw Invalid();
        RoutingKonfigurationRegeln.PruefeVersion(anfrage.Version);
        RoutingKonfigurationRegeln.PruefeDienst(anfrage.Dienst);
        return Send(JsonRequest(HttpMethod.Put, "api/v1/routing-konfiguration/dienst", anfrage), cancellationToken);
    }

    public Task<RoutingKonfigurationAntwort> AdresseSpeichern(RoutingAdresseAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null) throw Invalid();
        RoutingKonfigurationRegeln.PruefeVersion(anfrage.Version);
        _ = RoutingKonfigurationRegeln.Adresse(anfrage.Adresse);
        return Send(JsonRequest(HttpMethod.Put, "api/v1/routing-konfiguration/brouter-adresse", anfrage), cancellationToken);
    }

    private async Task<RoutingKonfigurationAntwort> Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
        {
            if (response.StatusCode == HttpStatusCode.BadRequest)
                throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Routingkonfiguration ungültig.");
            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new StammdatenException(StammdatenFehlerart.Konflikt, "Routingkonfiguration wurde zwischenzeitlich geändert.");
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Routingkonfiguration konnte nicht verarbeitet werden.", null, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<RoutingKonfigurationAntwort>(cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? throw new HttpRequestException("Routingkonfigurationantwort ungültig.");
            if (result.Version is not { Length: 64 } || !result.Version.All(Uri.IsHexDigit) || !Enum.IsDefined(result.Dienst) ||
                result.AdresseAusgeblendet && result.BRouterAdresse is not null ||
                !result.AdresseAusgeblendet && result.BRouterAdresse is null ||
                result.BRouterAdresse is not null && !RoutingKonfigurationRegeln.IstAdresseSichtbar(result.BRouterAdresse))
                throw new HttpRequestException("Routingkonfigurationantwort ungültig.");
            return result;
        }
    }

    private static HttpRequestMessage JsonRequest<T>(HttpMethod method, string path, T body) =>
        new(method, path) { Content = JsonContent.Create(body) };

    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, "Routingkonfiguration ungültig.");
}
