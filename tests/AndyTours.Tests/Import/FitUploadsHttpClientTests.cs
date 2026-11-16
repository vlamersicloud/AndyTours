using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Import;
using AndyTours.Application.Stammdaten;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Import;

public sealed class FitUploadsHttpClientTests
{
    [Fact]
    public async Task Hochladen_streamt_unicode_bytes_und_besitzt_caller_stream_nicht()
    {
        var bytes = Encoding.UTF8.GetBytes("synthetic-fit-body");
        var successContent = new TrackingContent(Json(new FitUploadAntwort(Guid.Parse("11111111-2222-3333-4444-555555555555"), "v1", "Grüße.fit", "hash", bytes.Length, FitUploadStatus.Hochgeladen, null, DateTimeOffset.UnixEpoch)).Content!);
        var handler = new RecordingHandler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/api/v1/fit-uploads/11111111-2222-3333-4444-555555555555", request.RequestUri!.AbsolutePath);
            Assert.Equal("Grüße.fit", request.Content!.Headers.ContentDisposition!.FileNameStar);
            Assert.Equal(bytes, await request.Content.ReadAsByteArrayAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = successContent };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new FitUploadsHttpClient(http);
        using var input = new MemoryStream(bytes);

        var result = await client.Hochladen(Guid.Parse("11111111-2222-3333-4444-555555555555"), "Grüße.fit", input, default);

        Assert.Equal("v1", result.Version);
        Assert.True(input.CanRead);
        Assert.Equal(1, handler.Count);
        Assert.True(successContent.WasDisposed);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge)]
    public async Task Fehlerantwort_ignoriert_malicious_body_und_stream_bleibt_offen(HttpStatusCode status)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new ExplodingContent() }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        using var input = new MemoryStream([1, 2, 3]);
        var client = new FitUploadsHttpClient(http);

        if (status == HttpStatusCode.Conflict)
            await Assert.ThrowsAsync<StammdatenException>(() => client.Hochladen(Guid.NewGuid(), "x.fit", input, default));
        else
            await Assert.ThrowsAsync<FitUploadZuGrossException>(() => client.Hochladen(Guid.NewGuid(), "x.fit", input, default));

        Assert.True(input.CanRead);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task Abbruch_propagiert_ohne_retry_und_get_404_gibt_null()
    {
        using var cts = new CancellationTokenSource();
        var handler = new RecordingHandler((_, ct) => { cts.Cancel(); ct.ThrowIfCancellationRequested(); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new FitUploadsHttpClient(http);
        using var input = new MemoryStream([9]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Hochladen(Guid.NewGuid(), "x.fit", input, cts.Token));
        Assert.True(input.CanRead);
        Assert.Equal(1, handler.Count);

        var notFound = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var http2 = new HttpClient(notFound) { BaseAddress = new Uri("https://unit.test/") };
        Assert.Null(await new FitUploadsHttpClient(http2).Laden(Guid.NewGuid(), default));
        Assert.Equal(1, notFound.Count);
    }

    private static HttpResponseMessage Json<T>(T value) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Count { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            return send(request, cancellationToken);
        }
    }

    private sealed class TrackingContent(HttpContent inner) : HttpContent
    {
        public bool WasDisposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => inner.CopyToAsync(stream);
        protected override bool TryComputeLength(out long length) { length = 0; return true; }
        protected override void Dispose(bool disposing) { WasDisposed = true; inner.Dispose(); base.Dispose(disposing); }
    }

    private sealed class ExplodingContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => throw new InvalidOperationException("body must not be read");
        protected override bool TryComputeLength(out long length) { length = 0; return true; }
        protected override Task<Stream> CreateContentReadStreamAsync() => throw new InvalidOperationException("body must not be read");
    }
}
