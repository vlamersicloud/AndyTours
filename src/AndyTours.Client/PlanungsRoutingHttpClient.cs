using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

public sealed class PlanungsRoutingHttpClient(HttpClient http) : IPlanungsRouting
{
    public async Task<PlanungsRoutingAntwort> Berechnen(PlanungsRoutingAnfrage anfrage, CancellationToken cancellationToken)
    {
        Validate(anfrage);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/planung/routing")
        {
            Content = JsonContent.Create(anfrage)
        };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Routinganfrage ungültig.");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Routinganfrage konnte nicht verarbeitet werden.", null, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<PlanungsRoutingAntwort>(cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? throw new HttpRequestException("Routingantwort ungültig.");
    }

    private static void Validate(PlanungsRoutingAnfrage request)
    {
        if (request is null || !Enum.IsDefined(request.Profil) || request.Von is null || request.Nach is null)
            throw Invalid();
        Validate(request.Von);
        Validate(request.Nach);
    }

    private static void Validate(PlanungsRoutingpunkt point)
    {
        if (point.Position is not { } p || !double.IsFinite(p.Breite) || p.Breite is < -90 or > 90 ||
            !double.IsFinite(p.Laenge) || p.Laenge is < -180 or > 180 ||
            point.Hoehe is { } h && !double.IsFinite(h)) throw Invalid();
    }

    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, "Routinganfrage ungültig.");
}
