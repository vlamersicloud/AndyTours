using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

public sealed class PlanungsProfilHttpClient(HttpClient http) : IPlanungsProfil
{
    public async Task<PlanungsProfilAntwort> Laden(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/planung/profil");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<PlanungsProfilAntwort>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new HttpRequestException("Profilantwort ungültig.");
    }

    public async Task<PlanungsProfilAntwort> Speichern(PlanungsProfilSpeichernAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage is null || anfrage.Version is null || anfrage.Version.Length != 64 ||
            !anfrage.Version.All(Uri.IsHexDigit) || !Enum.IsDefined(anfrage.Profil))
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Planungsprofil ungültig.");
        using var request = new HttpRequestMessage(HttpMethod.Put, "api/v1/planung/profil") { Content = JsonContent.Create(anfrage) };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<PlanungsProfilAntwort>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new HttpRequestException("Profilantwort ungültig.");
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Planungsprofil ungültig.");
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Planungsprofil wurde zwischenzeitlich geändert.");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Planungsprofil konnte nicht verarbeitet werden.", null, response.StatusCode);
    }
}
