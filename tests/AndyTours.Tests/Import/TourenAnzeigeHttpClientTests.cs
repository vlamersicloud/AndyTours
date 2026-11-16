using System.Net;
using System.Text.Json;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Import;

public sealed class TourenAnzeigeHttpClientTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private const string Version = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task List_roundtrips_display_projection_and_accepts_legacy_response_without_display()
    {
        var raw = new TourWerte(1, new DateOnly(2026, 1, 2), new TimeOnly(7, 8), "Tour",
            90061, 123.456, 0, 0, 0, 12.3, 0, 0, 0, 0, -5, 0, 0, 0, "GA1", "Note", 1, 2, []);
        var display = new TourAnzeigeAntwort(raw.Datum, raw.Startzeit, raw.Name, "Rad", "K", "Bike",
            123.456, 0, 90061, null, 12.3, 0, 0, 0, -5, 0, "GA1", "Note",
            true, false, true, false, true);
        var response = new TourAntwort(7, Version, raw) { Anzeige = display };
        var summary = new TourenTabellensummeAntwort(1, 123.46, 0, 90061);
        var legacyJson = JsonSerializer.Serialize(new
        {
            Eintraege = new[] { new { response.Id, response.Version, response.Werte } },
            Gesamt = 1, Start = 0, Anzahl = 1
        }, WebJson);

        var contents = new List<TrackingContent>();
        HttpRequestMessage? captured = null;
        using var handler = new Handler((request, _) =>
        {
            captured ??= request;
            var json = contents.Count == 0
                ? JsonSerializer.Serialize(new TourListeAntwort([response], 1, 0, 50) { Tabellensumme = summary }, WebJson)
                : legacyJson;
            var content = new TrackingContent(json);
            contents.Add(content);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new TourenHttpClient(http);

        var page = await client.ListeGefiltert(0, 50, new(null, null, null, null)
        { Sortierung = TourenSortierschluessel.Kommentar, Absteigend = true });
        var actual = Assert.Single(page.Eintraege);
        Assert.Equal(7, actual.Id);
        Assert.Equal(Version, actual.Version);
        Assert.Equal(JsonSerializer.Serialize(raw), JsonSerializer.Serialize(actual.Werte));
        Assert.Equal(display, actual.Anzeige);
        Assert.Equal(summary, page.Tabellensumme);
        Assert.Equal(new DateOnly(2026, 1, 2), actual.Anzeige!.Datum);
        Assert.Equal(new TimeOnly(7, 8), actual.Anzeige.Startzeit);
        Assert.Equal(90061, actual.Anzeige.FahrzeitSekunden);
        Assert.Equal(123.456, actual.Anzeige.DistanzKm);
        Assert.Null(actual.Anzeige.DurchschnittKmh);
        Assert.Equal(-5, actual.Anzeige.Temperatur);
        Assert.True(actual.Anzeige.IstBestDistanz);
        Assert.True(actual.Anzeige.IstBestFahrzeit);
        Assert.True(actual.Anzeige.IstBestMaxKmh);

        var legacyPage = await client.ListeGefiltert(0, 50, new(null, null, null, null));
        var legacy = Assert.Single(legacyPage.Eintraege);
        Assert.Null(legacyPage.Tabellensumme);
        Assert.Equal(7, legacy.Id);
        Assert.Equal(Version, legacy.Version);
        Assert.Equal(JsonSerializer.Serialize(raw), JsonSerializer.Serialize(legacy.Werte));
        Assert.Null(legacy.Anzeige);

        Assert.Equal(2, handler.Sends);
        Assert.All(contents, c => Assert.True(c.Disposed));
        Assert.Equal(HttpMethod.Get, captured!.Method);
        Assert.Equal("/api/v1/touren/", captured.RequestUri!.AbsolutePath);
        var query = ParseQuery(captured.RequestUri.Query);
        Assert.Equal("18", query["sortierung"]);
        Assert.Equal("true", query["absteigend"]);
        Assert.Null(captured.Content);
    }

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1]));

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public int Sends { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sends++;
            return respond(request, cancellationToken);
        }
    }

    private sealed class TrackingContent(string value) : StringContent(value, System.Text.Encoding.UTF8, "application/json")
    {
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}