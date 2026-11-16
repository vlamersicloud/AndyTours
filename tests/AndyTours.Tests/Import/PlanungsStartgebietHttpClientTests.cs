using System.Net;
using System.Net.Http.Json;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Import;

public sealed class PlanungsStartgebietHttpClientTests
{
    [Theory]
    [InlineData("{\"suedWest\":null,\"nordOst\":null}")]
    [InlineData("{\"suedWest\":{\"breite\":1,\"laenge\":2},\"nordOst\":{\"breite\":1,\"laenge\":2}}")]
    [InlineData("{\"suedWest\":{\"breite\":47,\"laenge\":10},\"nordOst\":{\"breite\":48,\"laenge\":11}}")]
    public async Task Accepts_null_pair_point_bounds_and_normal_bounds(string json)
    {
        using var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new TrackingContent(json) });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var result = await new PlanungsStartgebietHttpClient(http).Laden(default);
        Assert.Equal("/api/v1/planung/startgebiet", handler.Request!.RequestUri!.AbsolutePath);
        Assert.True(handler.Disposed);
        Assert.Equal(json.Contains("null", StringComparison.Ordinal), result.SuedWest is null);
    }

    [Theory]
    [InlineData("{\"suedWest\":null,\"nordOst\":{\"breite\":1,\"laenge\":2}}")]
    [InlineData("{\"suedWest\":{\"breite\":2,\"laenge\":2},\"nordOst\":{\"breite\":1,\"laenge\":1}}")]
    [InlineData("{\"suedWest\":{\"breite\":91,\"laenge\":2},\"nordOst\":{\"breite\":91,\"laenge\":2}}")]
    public async Task Rejects_invalid_bounds_and_does_not_read_error_body(string json)
    {
        using var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new TrackingContent(json) });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        await Assert.ThrowsAsync<HttpRequestException>(() => new PlanungsStartgebietHttpClient(http).Laden(default));
        Assert.True(handler.Disposed);

        var error = new TrackingContent("private backend detail");
        using var failedHandler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = error });
        using var failedHttp = new HttpClient(failedHandler) { BaseAddress = new Uri("https://unit.test/") };
        await Assert.ThrowsAsync<HttpRequestException>(() => new PlanungsStartgebietHttpClient(failedHttp).Laden(default));
        Assert.False(error.Read);
        Assert.True(failedHandler.Disposed);
    }

    [Fact]
    public async Task Forwards_cancellation_without_retry()
    {
        using var cts = new CancellationTokenSource();
        using var handler = new Handler(_ => throw new OperationCanceledException(cts.Token));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PlanungsStartgebietHttpClient(http).Laden(cts.Token));
        Assert.Equal(1, handler.Sends);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Sends { get; private set; }
        public HttpRequestMessage? Request { get; private set; }
        public bool Disposed { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Sends++; Request = request; cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(response(request)); }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class TrackingContent(string text) : HttpContent
    {
        public bool Read { get; private set; }
        public bool Disposed { get; private set; }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context) { Read = true; await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(text)); }
        protected override bool TryComputeLength(out long length) { length = System.Text.Encoding.UTF8.GetByteCount(text); return true; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
