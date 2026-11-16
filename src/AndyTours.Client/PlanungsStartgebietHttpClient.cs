using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

public sealed class PlanungsStartgebietHttpClient(HttpClient http) : IPlanungsStartgebiet
{
    public async Task<PlanungsStartgebietAntwort> Laden(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/planung/startgebiet");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Startgebiet konnte nicht geladen werden.", null, response.StatusCode);
        var answer = await response.Content.ReadFromJsonAsync<PlanungsStartgebietAntwort>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new HttpRequestException("Startgebietantwort ungültig.");
        if ((answer.SuedWest is null) != (answer.NordOst is null) ||
            (answer.SuedWest is not null && (!Valid(answer.SuedWest) || !Valid(answer.NordOst!) ||
             answer.SuedWest.Breite > answer.NordOst!.Breite || answer.SuedWest.Laenge > answer.NordOst.Laenge)))
            throw new HttpRequestException("Startgebietantwort ungültig.");
        return answer;
    }

    private static bool Valid(PlanungsPosition p) => double.IsFinite(p.Breite) && p.Breite is >= -90 and <= 90 &&
        double.IsFinite(p.Laenge) && p.Laenge is >= -180 and <= 180;
}
