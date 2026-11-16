using System.Net;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Import;

public sealed class PlanungsDateienHttpClientTests
{
    [Fact]
    public async Task Upload_and_export_use_typed_json_and_keep_caller_stream_open()
    {
        byte[] gpx = Encoding.UTF8.GetBytes("<gpx>Grüße</gpx>");
        var response1 = new TrackContent("{\"profil\":1,\"wegpunkte\":[{\"breite\":1,\"laenge\":2},{\"breite\":3,\"laenge\":4}],\"teilstuecke\":[null]}");
        var response2 = new TrackContent(JsonSerializer.Serialize(new PlanungsGpxExportAntwort("Café.gpx", gpx)));
        var handler = new FakeHandler(async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/lesen", StringComparison.Ordinal))
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("application/gpx+xml", request.Content!.Headers.ContentType!.MediaType);
                Assert.Equal(gpx, await request.Content.ReadAsByteArrayAsync(ct));
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = response1 };
            }
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            using var json = JsonDocument.Parse(await request.Content.ReadAsStringAsync(ct));
            Assert.Equal("Tour", json.RootElement.GetProperty("name").GetString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = response2 };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new PlanungsDateienHttpClient(http);
        using var input = new MemoryStream(gpx);
        var stand = await client.Lesen(input, default);
        Assert.True(input.CanRead);
        var request = new PlanungsGpxExportAnfrage("Tour", stand);
        var exported = await client.Schreiben(request, default);
        Assert.Equal("Café.gpx", exported.Dateiname);
        Assert.Equal(gpx, exported.Inhalt);
        Assert.True(response1.WasDisposed);
        Assert.True(response2.WasDisposed);
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task Error_body_is_not_read_and_cancellation_does_not_retry_or_close_stream()
    {
        var error = new ErrorContent();
        var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.RequestEntityTooLarge) { Content = error }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        using var input = new MemoryStream([1, 2]);
        await Assert.ThrowsAsync<PlanungsDateiZuGrossException>(() => new PlanungsDateienHttpClient(http).Lesen(input, default));
        Assert.False(error.WasRead); Assert.True(error.WasDisposed); Assert.True(input.CanRead); Assert.Equal(1, handler.Count);

        using var cts = new CancellationTokenSource();
        var cancelling = new FakeHandler((_, ct) => { cts.Cancel(); ct.ThrowIfCancellationRequested(); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); });
        using var http2 = new HttpClient(cancelling) { BaseAddress = new Uri("https://unit.test/") };
        using var input2 = new MemoryStream([1]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PlanungsDateienHttpClient(http2).Lesen(input2, cts.Token));
        Assert.True(input2.CanRead); Assert.Equal(1, cancelling.Count);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { public int Count { get; private set; } protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Count++; return send(request, cancellationToken); } }
    private sealed class TrackContent(string json) : StringContent(json, Encoding.UTF8, "application/json")
    { public bool WasDisposed { get; private set; } protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); } }
    private sealed class ErrorContent : HttpContent
    {
        public bool WasRead { get; private set; } public bool WasDisposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) { WasRead = true; throw new InvalidOperationException(); }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) { WasRead = true; throw new InvalidOperationException(); }
        protected override Task<Stream> CreateContentReadStreamAsync() { WasRead = true; throw new InvalidOperationException(); }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }
}
