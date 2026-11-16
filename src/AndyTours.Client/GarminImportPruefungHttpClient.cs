using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

public sealed class GarminImportPruefungHttpClient(HttpClient http) : IGarminImportPruefung
{
    private const string Root = "api/v1/garmin/importe";
    public Task<GarminImportPruefungAntwort> Laden(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (jobId == Guid.Empty) throw Invalid();
        return Get(jobId, cancellationToken);
    }
    private async Task<GarminImportPruefungAntwort> Get(Guid jobId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Root}/{jobId:D}/pruefung");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        await Check(response).ConfigureAwait(false);
        return await Read<GarminImportPruefungAntwort>(response, ct).ConfigureAwait(false);
    }
    public async Task<GarminImportUebernahmeAntwort> Uebernehmen(Guid jobId, GarminImportUebernahmeAnfrage anfrage, CancellationToken cancellationToken = default)
    {
        if (jobId == Guid.Empty || anfrage is null || anfrage.AnfrageId == Guid.Empty) throw Invalid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Root}/{jobId:D}/uebernehmen") { Content = JsonContent.Create(anfrage) };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await Check(response).ConfigureAwait(false);
        return await Read<GarminImportUebernahmeAntwort>(response, cancellationToken).ConfigureAwait(false);
    }
    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false) ?? throw new BackendApiException(response.StatusCode);
    private static Task Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return Task.CompletedTask;
        if (response.StatusCode == HttpStatusCode.BadRequest) throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Garmin-Importanfrage ist ungültig.");
        if (response.StatusCode == HttpStatusCode.NotFound) throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Garmin-Import wurde nicht gefunden.");
        if (response.StatusCode == HttpStatusCode.Conflict) throw new StammdatenException(StammdatenFehlerart.Konflikt, "Garmin-Import wurde zwischenzeitlich geändert.");
        throw new BackendApiException(response.StatusCode);
    }
    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, "Garmin-Importanfrage ist ungültig.");
}
