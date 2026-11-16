using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

/// <summary>HTTP-Implementierung des Touren-Persistenzports für das versionierte Backend.</summary>
public sealed class TourenHttpClient(HttpClient http) : ITourenSpeicher
{
    private const string Basis = "api/v1/touren/";

    /// <inheritdoc />
    public Task<TourListeAntwort> Liste(int start, int anzahl, DateOnly? von, DateOnly? bis, CancellationToken ct = default) =>
        ListeGefiltert(start, anzahl, new TourenListenfilter(von, bis, null, null), ct);

    /// <inheritdoc />
    public async Task<TourListeAntwort> ListeGefiltert(int start, int anzahl, TourenListenfilter filter, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (filter is null)
            throw new UngueltigeTourException("Tourenfilter fehlt.");
        if (!Enum.IsDefined(filter.Sortierung))
            throw new UngueltigeTourException("Tourensortierung ist ungültig.");
        var query = $"?start={start.ToString(CultureInfo.InvariantCulture)}&anzahl={anzahl.ToString(CultureInfo.InvariantCulture)}";
        if (filter.Von.HasValue) query += $"&von={Uri.EscapeDataString(filter.Von.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}";
        if (filter.Bis.HasValue) query += $"&bis={Uri.EscapeDataString(filter.Bis.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}";
        if (filter.AktivitaetId.HasValue) query += $"&aktivitaetId={filter.AktivitaetId.Value.ToString(CultureInfo.InvariantCulture)}";
        if (filter.KategorieId.HasValue) query += $"&kategorieId={filter.KategorieId.Value.ToString(CultureInfo.InvariantCulture)}";
        query += $"&sortierung={((int)filter.Sortierung).ToString(CultureInfo.InvariantCulture)}&absteigend={filter.Absteigend.ToString().ToLowerInvariant()}";
        using var request = new HttpRequestMessage(HttpMethod.Get, Basis + query);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        await Pruefe(response, ct);
        return await response.Content.ReadFromJsonAsync<TourListeAntwort>(cancellationToken: ct)
            ?? throw new BackendApiException(response.StatusCode);
    }

    /// <inheritdoc />
    public async Task<TourAntwort?> Lesen(long id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync(Basis + id.ToString(CultureInfo.InvariantCulture), ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await Pruefe(response, ct);
        return await response.Content.ReadFromJsonAsync<TourAntwort>(cancellationToken: ct)
            ?? throw new BackendApiException(response.StatusCode);
    }

    /// <inheritdoc />
    public Task<TourAntwort> Anlegen(TourWerte werte, CancellationToken ct = default) =>
        AnlegenIdempotent(werte, Guid.NewGuid(), ct);

    /// <inheritdoc />
    public async Task<TourAntwort> AnlegenIdempotent(TourWerte werte, Guid anfrageId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Basis)
        {
            Content = JsonContent.Create(werte)
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", anfrageId.ToString("D"));
        using var response = await http.SendAsync(request, ct);
        await Pruefe(response, ct);
        return await response.Content.ReadFromJsonAsync<TourAntwort>(cancellationToken: ct)
            ?? throw new BackendApiException(response.StatusCode);
    }

    /// <inheritdoc />
    public async Task<TourAntwort> Aendern(long id, TourAendernAnfrage anfrage, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync(Basis + id.ToString(CultureInfo.InvariantCulture), anfrage, ct);
        await Pruefe(response, ct);
        return await response.Content.ReadFromJsonAsync<TourAntwort>(cancellationToken: ct)
            ?? throw new BackendApiException(response.StatusCode);
    }

    /// <inheritdoc />
    public async Task Loeschen(long id, string version, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, Basis + id.ToString(CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        using var response = await http.SendAsync(request, ct);
        await Pruefe(response, ct);
    }

    private static async Task Pruefe(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                throw new UngueltigeTourException("Das Backend hat die Tourdaten abgelehnt.");
            case HttpStatusCode.NotFound:
                throw new TourNichtGefundenException(0);
            case HttpStatusCode.Conflict:
            case HttpStatusCode.PreconditionFailed:
                throw new TourVersionskonfliktException();
            default:
                throw new BackendApiException(response.StatusCode);
        }
    }
}

/// <summary>Sichere, statusbezogene Ausnahme bei nichtfachlichen Backendfehlern.</summary>
public sealed class BackendApiException(HttpStatusCode statusCode)
    : Exception($"Backend-Anfrage fehlgeschlagen (HTTP {(int)statusCode}).")
{
    /// <summary>HTTP-Statuscode der Antwort.</summary>
    public HttpStatusCode StatusCode { get; } = statusCode;
}
