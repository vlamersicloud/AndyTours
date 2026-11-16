using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

/// <summary>Typisierter HTTP-Client für die Wartungs- und Alarmverwaltung.</summary>
public sealed class WartungsverwaltungHttpClient(HttpClient http) : IWartungsverwaltung
{
    private const string Path = "api/v1/wartungsverwaltung/";

    /// <inheritdoc />
    public async Task<WartungsuebersichtAntwort> Laden(Wartungsfilter filter, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (filter.AktivitaetId is long activity) query.Add("aktivitaetId=" + Uri.EscapeDataString(activity.ToString(CultureInfo.InvariantCulture)));
        if (filter.EquipmentId is long equipment) query.Add("equipmentId=" + Uri.EscapeDataString(equipment.ToString(CultureInfo.InvariantCulture)));
        var url = Path + (query.Count == 0 ? "" : "?" + string.Join("&", query));
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        await Check(response, ct);
        return await Read<WartungsuebersichtAntwort>(response, ct);
    }

    /// <inheritdoc />
    public Task<WartungsbestandAntwort> WartungAnlegen(WartungSpeichernAnfrage anfrage, CancellationToken ct = default) => Send(HttpMethod.Post, "wartungen", anfrage, ct);
    /// <inheritdoc />
    public Task<WartungsbestandAntwort> WartungAendern(long id, WartungSpeichernAnfrage anfrage, CancellationToken ct = default) => Send(HttpMethod.Put, $"wartungen/{id.ToString(CultureInfo.InvariantCulture)}", anfrage, ct);
    /// <inheritdoc />
    public Task<WartungsbestandAntwort> AlarmAnlegen(AlarmSpeichernAnfrage anfrage, CancellationToken ct = default) => Send(HttpMethod.Post, "alarme", anfrage, ct);
    /// <inheritdoc />
    public Task<WartungsbestandAntwort> AlarmAendern(long id, AlarmSpeichernAnfrage anfrage, CancellationToken ct = default) => Send(HttpMethod.Put, $"alarme/{id.ToString(CultureInfo.InvariantCulture)}", anfrage, ct);
    /// <inheritdoc />
    public Task<WartungsbestandAntwort> Loeschen(WartungenAuswahlAnfrage anfrage, CancellationToken ct = default) => Send(HttpMethod.Post, "auswahl/loeschen", anfrage, ct);
    /// <inheritdoc />
    public Task<WartungsbestandAntwort> AlarmeUmschalten(WartungenAuswahlAnfrage anfrage, CancellationToken ct = default) => Send(HttpMethod.Post, "alarme/umschalten", anfrage, ct);

    private async Task<WartungsbestandAntwort> Send<T>(HttpMethod method, string path, T body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, Path + path) { Content = JsonContent.Create(body) };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        await Check(response, ct);
        return await Read<WartungsbestandAntwort>(response, ct);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct) ?? throw new BackendApiException(response.StatusCode);

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
        var fallback = (fallbackKind, "Wartungsanforderung konnte nicht verarbeitet werden.");
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
