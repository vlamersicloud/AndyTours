using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndyTours.Application.Stammdaten;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Tabellenlayout;

public sealed class EinheitentabellenlayoutHttpClientTests
{
    private const string Route = "/api/v1/einstellungen/einheitentabelle/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ExpectedJsonKeys = ["anordnung", "ausgeblendet", "version"];
    private static EinheitentabellenlayoutAntwort Valid() => new(new string('B', 64),
        Array.AsReadOnly(Enum.GetValues<Einheitentabellenschluessel>().Select(x => new Einheitentabellenspalte(x, null)).ToArray()),
        Array.AsReadOnly(Array.Empty<Einheitentabellenschluessel>()));
    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK) { Content = new TrackedContent(JsonSerializer.Serialize(Valid(), JsonOptions)) };

    [Fact]
    public async Task GetAndPutUseExactRouteMethodsCompleteJsonAndDisposeContent()
    {
        var handler = new TrackingHandler(_ => Ok());
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
        var client = new EinheitentabellenlayoutHttpClient(http);
        var loaded = await client.Laden(default);
        Assert.Equal(18, loaded.Anordnung.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(Route, handler.Requests[0].RequestUri!.AbsolutePath);
        var layout = Valid();
        await client.Speichern(new EinheitentabellenlayoutAnfrage(layout.Version, layout.Anordnung, layout.Ausgeblendet), default);
        Assert.Equal(HttpMethod.Put, handler.Requests[1].Method);
        Assert.Equal(Route, handler.Requests[1].RequestUri!.AbsolutePath);
        using var json = JsonDocument.Parse(handler.RequestBodies[1]!);
        Assert.Equal(ExpectedJsonKeys, json.RootElement.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal));
        Assert.Equal(18, json.RootElement.GetProperty("anordnung").GetArrayLength());
        var putContent = handler.RequestContents[1];
        Assert.NotNull(putContent);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => putContent!.ReadAsStringAsync());
        Assert.All(handler.ResponseContents, x => Assert.True(x.Disposed));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task ClientMapsTyped400And409WithoutReadingBodies(HttpStatusCode status)
    {
        var content = new UnbufferedContent("secret response");
        var handler = new TrackingHandler(_ => new HttpResponseMessage(status) { Content = content });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
        var ex = await Assert.ThrowsAsync<StammdatenException>(() => new EinheitentabellenlayoutHttpClient(http).Laden(default));
        Assert.Equal(status == HttpStatusCode.BadRequest ? StammdatenFehlerart.Ungueltig : StammdatenFehlerart.Konflikt, ex.Art);
        Assert.Equal(0, content.ReadCount);
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task ServiceUnavailableDoesNotReadBody()
    {
        var content = new UnbufferedContent("secret response");
        var handler = new TrackingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = content });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => new EinheitentabellenlayoutHttpClient(http).Laden(default));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Equal(0, content.ReadCount);
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task MalformedAndNullResponsesRejectedAndCallerClientRetained()
    {
        var validJson = JsonSerializer.Serialize(Valid(), JsonOptions);
        var nullList = JsonNode.Parse(validJson)!;
        nullList["anordnung"] = null;
        var nullEntry = JsonNode.Parse(validJson)!;
        nullEntry["anordnung"]![0] = null;
        foreach (var body in new[] { "{", nullList.ToJsonString(), nullEntry.ToJsonString() })
        {
            var handler = new TrackingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new TrackedContent(body) });
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
            var client = new EinheitentabellenlayoutHttpClient(http);
            await Assert.ThrowsAsync<HttpRequestException>(() => client.Laden(default));
            using var probe = await http.GetAsync("probe");
            Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
            Assert.False(handler.Disposed);
            http.Dispose();
            Assert.True(handler.Disposed);
        }
    }

    [Fact]
    public async Task PrecancellationDoesNotSendAndTransportFailureIsNotRetried()
    {
        var handler = new TrackingHandler(_ => throw new HttpRequestException("transport"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
        var client = new EinheitentabellenlayoutHttpClient(http);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Laden(cancelled.Token));
        Assert.Empty(handler.Requests);
        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => client.Laden(default));
        Assert.Equal("transport", failure.Message);
        Assert.Single(handler.Requests);
    }

    private sealed class TrackedContent(string value) : HttpContent
    {
        public bool Disposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(Encoding.UTF8.GetBytes(value)).AsTask();
        protected override bool TryComputeLength(out long length) { length = Encoding.UTF8.GetByteCount(value); return true; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class UnbufferedContent(string value) : HttpContent
    {
        public int ReadCount { get; private set; }
        public bool Disposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        { ReadCount++; return stream.WriteAsync(Encoding.UTF8.GetBytes(value)).AsTask(); }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class TrackingHandler(Func<HttpRequestMessage, HttpResponseMessage> make) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string?> RequestBodies { get; } = [];
        public List<HttpContent?> RequestContents { get; } = [];
        public List<TrackedContent> ResponseContents { get; } = [];
        public bool Disposed { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestContents.Add(request.Content);
            RequestBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            var response = make(request);
            if (response.Content is TrackedContent tracked) ResponseContents.Add(tracked);
            return response;
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
