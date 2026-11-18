using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests;

public sealed class ApiTourenFilterauswahlHttpClientTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    private static readonly TourenFilterauswahlAnfrage Filter = new(
        new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 2, 5,
        new TourenTabellenfilter(null, "GA1", "1", "25", "0", "500", "rad"));

    private static TourAntwort Tour(long id, char version = 'A') =>
        new(id, new string(version, 64),
            new TourWerte(1, new DateOnly(2026, 4, 12), null, $"Tour{id}", 900,
                12, 20, 800, 100, 30, 120, 150, 80, 80, 10, 0, 70, 0,
                "GA1", "", 2, null, [3, 7]));

    [Fact]
    public async Task SendsJsonToTheSelectionRouteAndReturnsReadonlyClonedSnapshotWithoutOwningHttpClient()
    {
        var responseBody = JsonSerializer.SerializeToUtf8Bytes(
            new TourenFilterauswahlAntwort(Filter, [Tour(10)]), Json);
        var handler = new FakeHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(responseBody) }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        var client = new TourenHttpClient(http);

        var response = await client.Auswaehlen(Filter);

        Assert.Equal(1, handler.Calls);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/api/v1/touren/auswahl/filtertreffer", handler.Path);
        var requestBody = Assert.IsType<string>(handler.RequestBody);
        Assert.Equal(Filter, JsonSerializer.Deserialize<TourenFilterauswahlAnfrage>(requestBody, Json));
        Assert.Equal(Filter, response.Filter);
        Assert.Equal(10, Assert.Single(response.Touren).Id);
        Assert.Equal(new long[] { 3, 7 }, response.Touren[0].Werte.KategorieIds);
        var readOnly = Assert.IsAssignableFrom<IList<TourAntwort>>(response.Touren);
        Assert.Throws<NotSupportedException>(() => readOnly.Add(Tour(11)));
        Assert.False(handler.Disposed);
        using var probe = await http.GetAsync("https://localhost/health");
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task PreCancelledRequestDoesNotReachTheHandler()
    {
        var handler = new FakeHandler((_, _) => throw new InvalidOperationException("Unexpected send."));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new TourenHttpClient(http).Auswaehlen(Filter, cts.Token));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(413)]
    [InlineData(503)]
    public async Task NonValidationErrorResponsesAreNotReadButAreDisposed(int statusCode)
    {
        var content = new TrackingContent(Encoding.UTF8.GetBytes("{\"private\":\"error body\"}"));
        var handler = new FakeHandler((_, _) => Task.FromResult(
            new HttpResponseMessage((HttpStatusCode)statusCode) { Content = content }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };

        var error = await Assert.ThrowsAsync<BackendApiException>(() =>
            new TourenHttpClient(http).Auswaehlen(Filter));

        Assert.Equal((HttpStatusCode)statusCode, error.StatusCode);
        Assert.Equal(0, content.Reads);
        Assert.True(content.Disposed);
        Assert.False(handler.Disposed);
    }

    [Fact]
    public async Task BadRequestIsRejectedWithoutReadingItsBody()
    {
        var content = new TrackingContent(Encoding.UTF8.GetBytes("{\"private\":\"error body\"}"));
        var handler = new FakeHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };

        await Assert.ThrowsAsync<AndyTours.Application.Touren.UngueltigeTourException>(() =>
            new TourenHttpClient(http).Auswaehlen(Filter));

        Assert.Equal(0, content.Reads);
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task NullMalformedMismatchedAndDuplicateResponsesAreRejected()
    {
        var otherFilter = Filter with { KategorieId = 99 };
        var invalidBodies = new[]
        {
            "null",
            "{",
            JsonSerializer.Serialize(new { filter = otherFilter, touren = new[] { Tour(1) } }, Json),
            JsonSerializer.Serialize(new { filter = Filter, touren = (TourAntwort[]?)null }, Json),
            JsonSerializer.Serialize(new { filter = Filter, touren = new[] { Tour(1), Tour(1, 'B') } }, Json),
            JsonSerializer.Serialize(new { filter = Filter, touren = new[] { Tour(0) } }, Json)
        };

        foreach (var body in invalidBodies)
        {
            var handler = new FakeHandler((_, _) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                }));
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                new TourenHttpClient(http).Auswaehlen(Filter));
            Assert.Equal(1, handler.Calls);
        }
    }

    [Fact]
    public async Task CancellationWhileWaitingForResponseDoesNotRetry()
    {
        var handler = new FakeHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        using var cts = new CancellationTokenSource();
        var pending = new TourenHttpClient(http).Auswaehlen(Filter, cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, handler.Calls);
    }

    private sealed class FakeHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool Disposed { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath;
            if (request.Content is not null)
                RequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return await responder(request, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class TrackingContent(byte[] bytes) : HttpContent
    {
        public int Reads { get; private set; }
        public bool Disposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            Reads++;
            return stream.WriteAsync(bytes, 0, bytes.Length);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = bytes.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Disposed = true;
            base.Dispose(disposing);
        }
    }
}
