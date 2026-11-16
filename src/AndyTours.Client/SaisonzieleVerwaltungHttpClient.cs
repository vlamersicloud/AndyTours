using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

/// <summary>Typisierter HTTP-Client für das versionierte Saisonziele.</summary>
public sealed class SaisonzieleVerwaltungHttpClient(HttpClient http) : ISaisonzieleVerwaltung
{
    private const string Path = "api/v1/saisonziele/";

    /// <inheritdoc />
    public async Task<SaisonzieleAntwort> Laden(CancellationToken ct = default)
    {
        using var response = await http.GetAsync(Path, HttpCompletionOption.ResponseHeadersRead, ct);
        await Check(response, ct);
        return await Read(response, ct);
    }

    /// <inheritdoc />
    public async Task<SaisonzieleAntwort> Speichern(SaisonzieleSpeichernAnfrage anfrage, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, Path) { Content = JsonContent.Create(anfrage) };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        await Check(response, ct);
        return await Read(response, ct);
    }

    private static async Task<SaisonzieleAntwort> Read(HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<SaisonzieleAntwort>(cancellationToken: ct)
        ?? throw new BackendApiException(response.StatusCode);

    private static async Task Check(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict)
        {
            var (kind, message) = await ReadProblem(response, ct);
            throw new StammdatenException(kind, message);
        }
        throw new BackendApiException(response.StatusCode);
    }

    private static async Task<(StammdatenFehlerart Art, string Meldung)> ReadProblem(HttpResponseMessage response, CancellationToken ct)
    {
        var fallbackKind = response.StatusCode switch
        {
            HttpStatusCode.NotFound => StammdatenFehlerart.NichtGefunden,
            HttpStatusCode.Conflict => StammdatenFehlerart.Konflikt,
            _ => StammdatenFehlerart.Ungueltig
        };
        var fallback = (fallbackKind, "Saisonzielanforderung konnte nicht verarbeitet werden.");
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[8193]; var count = 0;
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
            var detail = detailElement.GetString();
            if (detail is null || detail.Length > 2000) return fallback;
            var expected = codeElement.GetString() switch
            {
                "stammdaten.ungueltig" => (StammdatenFehlerart.Ungueltig, HttpStatusCode.BadRequest),
                "stammdaten.nichtgefunden" => (StammdatenFehlerart.NichtGefunden, HttpStatusCode.NotFound),
                "stammdaten.konflikt" => (StammdatenFehlerart.Konflikt, HttpStatusCode.Conflict),
                _ => ((StammdatenFehlerart Art, HttpStatusCode Status)?)null
            };
            if (expected is null || expected.Value.Status != response.StatusCode) return fallback;
            return (expected.Value.Art, detail);
        }
        catch (Exception e) when (e is JsonException or IOException or NotSupportedException) { return fallback; }
    }
}
