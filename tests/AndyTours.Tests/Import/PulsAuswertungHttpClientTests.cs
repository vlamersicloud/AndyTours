using System.Net;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Import;

public sealed class PulsAuswertungHttpClientTests
{
    [Fact]
    public async Task TypedReferenceAndAnalysisRequestsPreserveServerValues()
    {
        const string version = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
        var reference = new PulsReferenzAntwort(version,
            new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero),
            [new PulsprofilAntwort("CYCLING", [90, 110, 130, 150, 170])]);
        var analysis = new PulsAuswertungAntwort(new(2026, 1, 10), new(2026, 1, 31),
            Auswertungsraster.Monat, 2, 1, 20, 900, reference, "Radfahren-É",
            [new PulsperiodeAntwort(new(2026, 1, 10), new(2026, 1, 31), "Jan 2026") { Schluessel = new(2026, 1, 1) }],
            [new PulsbereichAntwort(1, "Zone 1", "90–109 bpm")],
            [new PulszelleAntwort(new(2026, 1, 1), 1, 20, 1)]);
        var contents = new[]
        {
            new TrackingContent(JsonSerializer.Serialize(reference)),
            new TrackingContent(JsonSerializer.Serialize(reference)),
            new TrackingContent(JsonSerializer.Serialize(analysis))
        };
        var handler = new RecordingHandler(async (request, ct) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("/api/v1/auswertung/puls/referenz", request.RequestUri!.AbsolutePath);
                Assert.Null(request.Content);
                return Ok(contents[0]);
            }
            var body = await request.Content!.ReadAsStringAsync(ct);
            if (request.RequestUri!.AbsolutePath.EndsWith("/referenz/aktualisieren", StringComparison.Ordinal))
            {
                Assert.Equal(version, JsonDocument.Parse(body).RootElement.GetProperty("Version").GetString());
                return Ok(contents[1]);
            }
            using var json = JsonDocument.Parse(body);
            Assert.Equal("Radfahren-É", json.RootElement.GetProperty("ProfilSport").GetString());
            Assert.Equal("2026-01-10", json.RootElement.GetProperty("Von").GetString());
            Assert.Equal(2, json.RootElement.GetProperty("Raster").GetInt32());
            Assert.Equal(7, json.RootElement.GetProperty("AktivitaetId").GetInt64());
            Assert.Equal(9, json.RootElement.GetProperty("KategorieId").GetInt64());
            return Ok(contents[2]);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new PulsAuswertungHttpClient(http);

        var gotReference = await client.LadenReferenz(CancellationToken.None);
        var refreshed = await client.AktualisierenReferenz(new(version), CancellationToken.None);
        var gotAnalysis = await client.Auswerten(new(new(2026, 1, 10), new(2026, 1, 31),
            Auswertungsraster.Monat, 7, 9, "Radfahren-É"), CancellationToken.None);

        Assert.Equal(reference.Version, gotReference.Version);
        Assert.Equal(refreshed.Profile[0].Untergrenzen, gotAnalysis.Referenz.Profile[0].Untergrenzen);
        Assert.Equal(new DateOnly(2026, 1, 1), gotAnalysis.Perioden[0].Schluessel);
        Assert.Equal(3, handler.Count);
        Assert.All(contents, content => Assert.True(content.WasDisposed));
    }

    [Fact]
    public async Task MapsSafe400409AndRejectsInvalidInputBeforeSend()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
        { Content = new ExplodingContent() }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new PulsAuswertungHttpClient(http);
        await Assert.ThrowsAsync<StammdatenException>(() => client.AktualisierenReferenz(
            new(new string('A', 64)), CancellationToken.None));
        Assert.Equal(1, handler.Count);
        var error = await Assert.ThrowsAsync<StammdatenException>(() => client.Auswerten(
            new(new(2026, 2, 1), new(2026, 1, 1), Auswertungsraster.Monat, null, null, null),
            CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.Konflikt, error.Art);
        Assert.Equal(1, handler.Count);
        Assert.DoesNotContain("secret", error.Message);
    }

    private static HttpResponseMessage Ok(HttpContent content) => new(HttpStatusCode.OK) { Content = content };

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Count { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            return send(request, cancellationToken);
        }
    }

    private sealed class TrackingContent(string json) : StringContent(json, Encoding.UTF8, "application/json")
    {
        public bool WasDisposed { get; private set; }
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }

    private sealed class ExplodingContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
            throw new InvalidOperationException("must not read error body");
        protected override bool TryComputeLength(out long length) { length = 0; return true; }
        protected override Task<Stream> CreateContentReadStreamAsync() =>
            throw new InvalidOperationException("must not read error body");
    }
}
