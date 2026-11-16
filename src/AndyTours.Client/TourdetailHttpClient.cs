using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
namespace AndyTours.Client;

public sealed class TourdetailHttpClient(HttpClient http) : ITourdetailLeser
{
    public async Task<TourdetailAntwort> Laden(TourdetailAnfrage anfrage, CancellationToken cancellationToken)
    {
        if (anfrage?.TourIds is null || anfrage.TourIds.Count is < 1 or > 10000 || anfrage.TourIds.Any(id => id <= 0) || anfrage.TourIds.Distinct().Count() != anfrage.TourIds.Count)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Tourdetail-Anfrage ungültig.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/tourdetails") { Content = JsonContent.Create(anfrage) };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.BadRequest) throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Tourdetail-Anfrage ungültig.");
        if (response.StatusCode == HttpStatusCode.NotFound) throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Tourdetail nicht gefunden.");
        if (!response.IsSuccessStatusCode) throw new BackendApiException(response.StatusCode);
        return await response.Content.ReadFromJsonAsync<TourdetailAntwort>(cancellationToken: cancellationToken).ConfigureAwait(false) ?? throw new BackendApiException(response.StatusCode);
    }
}
