using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

/// <summary>Typed client for durable Garmin job actions; does not expose worker leases.</summary>
public sealed class GarminImportJobsHttpClient(HttpClient http) : IGarminImportJobs
{
    private const string Root = "api/v1/garmin/importe";

    public Task<GarminImportJobAntwort> Starten(GarminImportStartAnfrage anfrage, CancellationToken ct = default) =>
        Send<GarminImportStartAnfrage, GarminImportJobAntwort>(HttpMethod.Post, Root + "/", anfrage, ct);

    public async Task<GarminImportJobAntwort?> Laden(Guid jobId, CancellationToken ct = default)
    {
        if (jobId == Guid.Empty) throw Invalid();
        using var response = await http.GetAsync($"{Root}/{jobId:D}", HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await Check(response).ConfigureAwait(false);
        return await Read<GarminImportJobAntwort>(response, ct).ConfigureAwait(false);
    }

    public async Task<GarminImportJobAntwort?> AktuellenLaden(CancellationToken ct = default)
    {
        using var response = await http.GetAsync(Root + "/aktuell", HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        await Check(response).ConfigureAwait(false);
        return await Read<GarminImportJobAntwort>(response, ct).ConfigureAwait(false);
    }

    public async Task<GarminImportKandidatenAntwort> Kandidaten(Guid jobId, int start, int anzahl, CancellationToken ct = default)
    {
        if (jobId == Guid.Empty || start < 0 || anzahl is < 1 or > 200) throw Invalid();
        var query = $"{Root}/{jobId:D}/kandidaten?start={start.ToString(CultureInfo.InvariantCulture)}&anzahl={anzahl.ToString(CultureInfo.InvariantCulture)}";
        using var response = await http.GetAsync(query, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        await Check(response).ConfigureAwait(false);
        return await Read<GarminImportKandidatenAntwort>(response, ct).ConfigureAwait(false);
    }

    public Task<GarminImportJobAntwort> Fortsetzen(Guid jobId, GarminImportSteuerAnfrage anfrage, CancellationToken ct = default) =>
        Control(jobId, "fortsetzen", anfrage, ct);

    public Task<GarminImportJobAntwort> Abbrechen(Guid jobId, GarminImportSteuerAnfrage anfrage, CancellationToken ct = default) =>
        Control(jobId, "abbrechen", anfrage, ct);

    private Task<GarminImportJobAntwort> Control(Guid jobId, string action, GarminImportSteuerAnfrage request, CancellationToken ct)
    {
        if (jobId == Guid.Empty || request is null) throw Invalid();
        return Send<GarminImportSteuerAnfrage, GarminImportJobAntwort>(HttpMethod.Post, $"{Root}/{jobId:D}/{action}", request, ct);
    }

    private async Task<TResponse> Send<TRequest, TResponse>(HttpMethod method, string path, TRequest body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        await Check(response).ConfigureAwait(false);
        return await Read<TResponse>(response, ct).ConfigureAwait(false);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false)
        ?? throw new BackendApiException(response.StatusCode);

    private static Task Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return Task.CompletedTask;
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Garmin-Importanfrage ist ungültig.");
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Garmin-Import wurde nicht gefunden.");
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Garmin-Import wurde zwischenzeitlich geändert.");
        throw new BackendApiException(response.StatusCode);
    }

    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, "Garmin-Importanfrage ist ungültig.");
}
