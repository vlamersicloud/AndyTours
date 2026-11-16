using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Import;

public sealed class TourenTabellenfilterHttpClientTests
{
    [Fact]
    public async Task Sends_exact_raw_table_values_and_omits_null_or_empty_filter_objects()
    {
        var responses = new List<TrackingContent>();
        var requests = new List<(Uri Uri, HttpMethod Method, bool HasBody)>();
        using var handler = new Handler((request, _) =>
        {
            requests.Add((request.RequestUri!, request.Method, request.Content is not null));
            var query = ParseQuery(request.RequestUri!.Query);
            var body = new TrackingContent(JsonSerializer.Serialize(new TourListeAntwort([], 0, int.Parse(query["start"], CultureInfo.InvariantCulture), int.Parse(query["anzahl"], CultureInfo.InvariantCulture))));
            responses.Add(body);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = body });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new TourenHttpClient(http);

        var raw = new TourenTabellenfilter(
            "Rad & Plus+ / Ä (ausgemustert)", "GA 1", "12,5", "20.25", "0", "ungültig", "Sabine+ Alm & Name");
        await client.ListeGefiltert(4, 8, new(new DateOnly(2026, 1, 2), new DateOnly(2026, 2, 3), 17, 23)
        {
            Sortierung = TourenSortierschluessel.Kommentar,
            Absteigend = true,
            Tabellenfilter = raw
        });
        var first = requests[0];
        var query = ParseQuery(first.Uri.Query);
        Assert.Equal("/api/v1/touren/", first.Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Get, first.Method);
        Assert.False(first.HasBody);
        Assert.Equal("4", query["start"]);
        Assert.Equal("8", query["anzahl"]);
        Assert.Equal("2026-01-02", query["von"]);
        Assert.Equal("2026-02-03", query["bis"]);
        Assert.Equal("17", query["aktivitaetId"]);
        Assert.Equal("23", query["kategorieId"]);
        Assert.Equal("18", query["sortierung"]);
        Assert.Equal("true", query["absteigend"]);
        Assert.Equal("Rad & Plus+ / Ä (ausgemustert)", query["equipmentName"]);
        Assert.Equal("GA 1", query["trainingsbereich"]);
        Assert.Equal("12,5", query["distanzVon"]);
        Assert.Equal("20.25", query["distanzBis"]);
        Assert.Equal("0", query["hoehenmeterVon"]);
        Assert.Equal("ungültig", query["hoehenmeterBis"]);
        Assert.Equal("Sabine+ Alm & Name", query["suchbegriff"]);

        var baseFilter = new TourenListenfilter(new DateOnly(2026, 1, 2), new DateOnly(2026, 2, 3), 17, 23)
        { Sortierung = TourenSortierschluessel.Kommentar, Absteigend = true };
        await client.ListeGefiltert(4, 8, baseFilter with { Tabellenfilter = null });
        await client.ListeGefiltert(4, 8, baseFilter with { Tabellenfilter = new TourenTabellenfilter() });
        await client.ListeGefiltert(4, 8, baseFilter with { Tabellenfilter = new TourenTabellenfilter(EquipmentName: string.Empty) });
        Assert.Equal(4, handler.Sends);
        Assert.Equal(4, responses.Count);
        foreach (var request in requests)
        {
            var oldQuery = ParseQuery(request.Uri.Query);
            Assert.Equal("/api/v1/touren/", request.Uri.AbsolutePath);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.False(request.HasBody);
            Assert.Equal("4", oldQuery["start"]);
            Assert.Equal("8", oldQuery["anzahl"]);
            Assert.Equal("2026-01-02", oldQuery["von"]);
            Assert.Equal("2026-02-03", oldQuery["bis"]);
            Assert.Equal("17", oldQuery["aktivitaetId"]);
            Assert.Equal("23", oldQuery["kategorieId"]);
            Assert.Equal("18", oldQuery["sortierung"]);
            Assert.Equal("true", oldQuery["absteigend"]);
        }
        var tableKeys = new[] { "equipmentName", "trainingsbereich", "distanzVon", "distanzBis", "hoehenmeterVon", "hoehenmeterBis", "suchbegriff" };
        foreach (var request in requests.Skip(1).Take(2))
        {
            var oldQuery = ParseQuery(request.Uri.Query);
            foreach (var key in tableKeys)
                Assert.False(oldQuery.ContainsKey(key));
        }
        var emptyValueQuery = ParseQuery(requests[3].Uri.Query);
        Assert.Equal(string.Empty, emptyValueQuery["equipmentName"]);
        foreach (var key in tableKeys.Skip(1))
            Assert.False(emptyValueQuery.ContainsKey(key));
        Assert.All(responses, response => Assert.True(response.Disposed));
    }

    private static Dictionary<string, string> ParseQuery(string query) => query.TrimStart('?').Split('&')
        .Select(part => part.Split('=', 2))
        .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1]));

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Sends { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Sends++; return send(request, cancellationToken); }
    }

    private sealed class TrackingContent(string json) : HttpContent
    {
        public bool Disposed { get; private set; }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            await stream.WriteAsync(Encoding.UTF8.GetBytes(json));
        protected override bool TryComputeLength(out long length) { length = Encoding.UTF8.GetByteCount(json); return true; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}