namespace AndyTours.Tests.Import;

using System.Net;
using System.Text;
using System.Text.Json;
using AndyTours.Client;
using AndyTours.Contracts.V1;
using Xunit;

public sealed class SaisonfortschrittHttpClientTests
{
    [Fact]
    public async Task Optional_season_projection_is_deserialized_and_older_response_remains_compatible()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var sut = new AktivitaetsauswertungHttpClient(client);
        var von = new DateOnly(2024, 1, 1);
        var bis = new DateOnly(2024, 2, 29);
        var request = new AktivitaetsauswertungAnfrage(von, bis, Auswertungsraster.Monat, 7, 9);

        var first = await sut.Laden(request);
        Assert.Equal(12.5, first.Saisonfortschritt!.Kilometer.Stand);
        Assert.Null(first.Saisonfortschritt.Kilometer.Ziel);
        Assert.Equal(200d, first.Saisonfortschritt.Hoehenmeter.Stand);
        Assert.Equal(3d, first.Saisonfortschritt.Anzahl.Stand);
        Assert.Equal(25.5, first.Saisonfortschritt.FahrzeitStunden.Stand);
        Assert.Null(first.Saisonfortschritt.FahrzeitStunden.Ziel);
        Assert.Equal(bis, first.Saisonfortschritt.Stichtag);

        var second = await sut.Laden(request);
        Assert.Null(second.Saisonfortschritt);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, captured =>
        {
            Assert.Equal(HttpMethod.Get, captured.Method);
            Assert.Equal("/api/v1/auswertung/aktivitaeten", captured.Path);
            Assert.Contains("von=2024-01-01", captured.Query, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("bis=2024-02-29", captured.Query, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("raster=Monat", captured.Query, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("aktivitaetId=7", captured.Query, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("kategorieId=9", captured.Query, StringComparison.OrdinalIgnoreCase);
            Assert.Null(captured.Body);
            Assert.True(captured.ResponseDisposed);
        });
        Assert.Equal(2, handler.ResponseCount);
        Assert.False(handler.Disposed);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];
        public int ResponseCount { get; private set; }
        public bool Disposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var record = new CapturedRequest(request.Method, uri.AbsolutePath, uri.Query,
                request.Content is null ? null : "unexpected");
            Requests.Add(record);

            object payload = Requests.Count == 1
                ? new AktivitaetsauswertungAntwort(new(2024, 1, 1), new(2024, 2, 29), Auswertungsraster.Monat,
                    3, 35, 350, 6300, 0, [])
                    {
                        Saisonfortschritt = new SaisonfortschrittAntwort(new(2024, 2, 29),
                            new(12.5, null, null, null, null, null, null),
                            new(200, null, null, null, null, null, null),
                            new(3, null, null, null, null, null, null),
                            new(25.5, null, null, null, null, null, null))
                    }
                : new
                {
                    Von = new DateOnly(2024, 1, 1),
                    Bis = new DateOnly(2024, 2, 29),
                    Raster = Auswertungsraster.Monat,
                    Anzahl = 3,
                    DistanzKm = 35d,
                    Hoehenmeter = 350L,
                    FahrzeitSekunden = 6300L,
                    ZukuenftigeAktivitaeten = 0,
                    Perioden = Array.Empty<object>()
                };

            var json = JsonSerializer.Serialize(payload);
            var content = new TrackingContent(json, () => record.ResponseDisposed = true);
            ResponseCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class TrackingContent(string json, Action disposed) : StringContent(json, Encoding.UTF8, "application/json")
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing) disposed();
            base.Dispose(disposing);
        }
    }

    private sealed class CapturedRequest(HttpMethod method, string path, string query, string? body)
    {
        public HttpMethod Method { get; } = method;
        public string Path { get; } = path;
        public string Query { get; } = query;
        public string? Body { get; } = body;
        public bool ResponseDisposed { get; set; }
    }
}
