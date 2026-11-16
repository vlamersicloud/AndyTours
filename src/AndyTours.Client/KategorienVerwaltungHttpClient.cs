using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

/// <summary>Typisierter HTTP-Client für versionierte Kategorienverwaltung.</summary>
public sealed class KategorienVerwaltungHttpClient(HttpClient http) : IKategorienVerwaltung
{
    private const string Basis = "api/v1/kategorien/";

    public async Task<KategorienAntwort> Laden(CancellationToken ct = default)
    {
        using var response = await http.GetAsync(Basis, ct);
        await Pruefe(response, ct);
        return await LiesAntwort(response, ct);
    }

    public Task<KategorienAntwort> Anlegen(KategorieSpeichernAnfrage anfrage, CancellationToken ct = default) =>
        SendeJson(HttpMethod.Post, Basis, anfrage, ct);

    public Task<KategorienAntwort> Umbenennen(long id, KategorieSpeichernAnfrage anfrage, CancellationToken ct = default) =>
        SendeJson(HttpMethod.Put, Basis + Id(id) + "/name", anfrage, ct);

    public async Task<KategorienAntwort> Loeschen(long id, string version, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, Basis + Id(id));
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        using var response = await http.SendAsync(request, ct);
        await Pruefe(response, ct);
        return await LiesAntwort(response, ct);
    }

    public Task<KategorienAntwort> SetzeStandard(KategorieStandardAnfrage anfrage, CancellationToken ct = default) =>
        SendeJson(HttpMethod.Put, Basis + "standard", anfrage, ct);

    public Task<KategorienAntwort> Zusammenfuehren(KategorienZusammenfuehrenAnfrage anfrage, CancellationToken ct = default) =>
        SendeJson(HttpMethod.Post, Basis + "zusammenfuehren", anfrage, ct);

    private async Task<KategorienAntwort> SendeJson<T>(HttpMethod methode, string pfad, T anfrage, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(methode, pfad) { Content = JsonContent.Create(anfrage) };
        using var response = await http.SendAsync(request, ct);
        await Pruefe(response, ct);
        return await LiesAntwort(response, ct);
    }

    private static async Task<KategorienAntwort> LiesAntwort(HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<KategorienAntwort>(cancellationToken: ct)
        ?? throw new BackendApiException(response.StatusCode);

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static async Task Pruefe(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict)
        {
            var (art, meldung) = await LiesFachfehler(response, ct);
            throw new StammdatenException(art, meldung);
        }
        throw new BackendApiException(response.StatusCode);
    }

    private static async Task<(StammdatenFehlerart Art, string Meldung)> LiesFachfehler(HttpResponseMessage response, CancellationToken ct)
    {
        var fallbackArt = response.StatusCode switch
        {
            HttpStatusCode.NotFound => StammdatenFehlerart.NichtGefunden,
            HttpStatusCode.Conflict => StammdatenFehlerart.Konflikt,
            _ => StammdatenFehlerart.Ungueltig
        };
        var fallback = (fallbackArt, "Kategorienanforderung konnte nicht verarbeitet werden.");
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[8193];
            var count = 0;
            while (count < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(count, buffer.Length - count), ct);
                if (read == 0) break;
                count += read;
            }
            if (count > 8192) return fallback;
            using var json = JsonDocument.Parse(buffer.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 16 });
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty("code", out var codeElement) || codeElement.ValueKind != JsonValueKind.String ||
                !json.RootElement.TryGetProperty("detail", out var detailElement) || detailElement.ValueKind != JsonValueKind.String)
                return fallback;
            var code = codeElement.GetString();
            var detail = detailElement.GetString();
            if (detail is null || detail.Length > 2000) return fallback;
            var expected = code switch
            {
                "stammdaten.ungueltig" => (StammdatenFehlerart.Ungueltig, HttpStatusCode.BadRequest),
                "stammdaten.nichtgefunden" => (StammdatenFehlerart.NichtGefunden, HttpStatusCode.NotFound),
                "stammdaten.konflikt" => (StammdatenFehlerart.Konflikt, HttpStatusCode.Conflict),
                _ => ((StammdatenFehlerart Art, HttpStatusCode Status)?)null
            };
            if (expected is null || expected.Value.Status != response.StatusCode) return fallback;
            return (expected.Value.Art, detail);
        }
        catch (Exception e) when (e is JsonException or IOException or NotSupportedException)
        {
            return fallback;
        }
    }
}
