namespace AndyTours.Tests.Import;

using System.Net;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Client;
using AndyTours.Contracts.V1;
using Xunit;

public sealed class SicherungsarchivErstellungHttpClientTests
{
    private static readonly Guid OperationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const string Hash = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
    private static readonly string[] ExpectedRequestProperties = ["vorgangsId"];

    [Fact]
    public async Task PostsOnlyOperationIdAndReturnsValidIdempotentResultsWithoutOwningCallerClient()
    {
        var answer = new SicherungsarchivErstellungsantwort(new SicherungAntwort(OperationId, Sicherungsart.Vollarchiv,
            new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero), 42, Hash), false);
        var retry = answer with { BereitsVorhanden = true };
        var handler = new FakeHandler(_ => Task.FromResult(JsonResponse(answer)), _ => Task.FromResult(JsonResponse(retry)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new SicherungsarchivErstellungHttpClient(http);
        Assert.Equal(answer, await client.Erstellen(new SicherungsarchivErstellungsanfrage(OperationId), default));
        Assert.Equal(retry, await client.Erstellen(new SicherungsarchivErstellungsanfrage(OperationId), default));
        Assert.Equal(2, handler.Sends);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("POST", request.Method);
            Assert.Equal("/api/v1/sicherungen/", request.Path);
            using var document = JsonDocument.Parse(request.Body!);
            Assert.Equal(ExpectedRequestProperties, document.RootElement.EnumerateObject().Select(p => p.Name));
            Assert.Equal(OperationId, document.RootElement.GetProperty("vorgangsId").GetGuid());
        });
        Assert.All(handler.Contents, content => Assert.True(content.Disposed));
        Assert.Equal(2, handler.RequestContents.Count);
        foreach (var content in handler.RequestContents)
            await Assert.ThrowsAsync<ObjectDisposedException>(() => content.ReadAsStringAsync());
        Assert.False(handler.Disposed);
        using var probe = await http.GetAsync("unused");
        Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
        Assert.Equal(3, handler.Sends);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, StammdatenFehlerart.Ungueltig)]
    [InlineData(HttpStatusCode.Conflict, StammdatenFehlerart.Konflikt)]
    public async Task MapsValidationAndConflictWithoutReadingErrorBody(HttpStatusCode status, StammdatenFehlerart kind)
    {
        var body = new UnreadableContent("SECRET_ERROR_BODY");
        var handler = new FakeHandler(_ => Task.FromResult(new HttpResponseMessage(status) { Content = body }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var ex = await Assert.ThrowsAsync<StammdatenException>(() =>
            new SicherungsarchivErstellungHttpClient(http).Erstellen(new SicherungsarchivErstellungsanfrage(OperationId), default));
        Assert.Equal(kind, ex.Art);
        Assert.DoesNotContain("SECRET_ERROR_BODY", ex.Message, StringComparison.Ordinal);
        Assert.False(body.Read);
        Assert.True(body.Disposed);
        Assert.Equal(1, handler.Sends);
    }

    [Fact]
    public async Task MapsUnavailableAndUnexpectedStatusWithoutReadingBody()
    {
        foreach (var status in new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.InternalServerError })
        {
            var body = new UnreadableContent("SECRET_ERROR_BODY");
            var handler = new FakeHandler(_ => Task.FromResult(new HttpResponseMessage(status) { Content = body }));
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                new SicherungsarchivErstellungHttpClient(http).Erstellen(new SicherungsarchivErstellungsanfrage(OperationId), default));
            Assert.Equal(status, ex.StatusCode);
            Assert.DoesNotContain("SECRET_ERROR_BODY", ex.Message, StringComparison.Ordinal);
            Assert.False(body.Read);
            Assert.True(body.Disposed);
        }
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("""{"sicherung":null,"bereitsVorhanden":false}""")]
    [InlineData("""{"sicherung":{"id":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","art":1,"erstelltUtc":"2026-11-17T12:00:00Z","byteanzahl":42,"inhaltHash":"0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"},"bereitsVorhanden":false}""")]
    [InlineData("""{"sicherung":{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","art":0,"erstelltUtc":"2026-11-17T12:00:00Z","byteanzahl":42,"inhaltHash":"0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"},"bereitsVorhanden":false}""")]
    [InlineData("""{"sicherung":{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","art":1,"erstelltUtc":null,"byteanzahl":42,"inhaltHash":"0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"},"bereitsVorhanden":false}""")]
    [InlineData("""{"sicherung":{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","art":1,"erstelltUtc":"2026-11-17T12:00:00+01:00","byteanzahl":42,"inhaltHash":"0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"},"bereitsVorhanden":false}""")]
    [InlineData("""{"sicherung":{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","art":1,"erstelltUtc":"2026-11-17T12:00:00Z","byteanzahl":0,"inhaltHash":"0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"},"bereitsVorhanden":false}""")]
    [InlineData("""{"sicherung":{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","art":1,"erstelltUtc":"2026-11-17T12:00:00Z","byteanzahl":42,"inhaltHash":"bad"},"bereitsVorhanden":false}""")]
    [InlineData("""{"sicherung":{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","art":1,"erstelltUtc":"2026-11-17T12:00:00Z","byteanzahl":42,"inhaltHash":"0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF","legacyZeitstempel":"2026-01-01_00-00-00"},"bereitsVorhanden":false}""")]
    [InlineData("""{"sicherung":{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","art":1,"erstelltUtc":"2026-11-17T12:00:00Z","byteanzahl":42,"inhaltHash":"0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"}}""")]
    [InlineData("""{"sicherung":{"art":1,"erstelltUtc":"2026-11-17T12:00:00Z","byteanzahl":42,"inhaltHash":"0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"},"bereitsVorhanden":false}""")]
    [InlineData("""{}""")]
    public async Task RejectsInvalidOrMalformedMetadataWithoutEchoingPayload(string payload)
    {
        var handler = new FakeHandler(_ => Task.FromResult(RawResponse(payload)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new SicherungsarchivErstellungHttpClient(http).Erstellen(new SicherungsarchivErstellungsanfrage(OperationId), default));
        Assert.Contains("ungültig", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(payload, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsEmptyIdAndPreCancellationWithoutSending()
    {
        var handler = new FakeHandler(_ => Task.FromResult(JsonResponse(new SicherungsarchivErstellungsantwort(
            new SicherungAntwort(OperationId, Sicherungsart.Vollarchiv, new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero), 1, Hash), false))));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        await Assert.ThrowsAsync<StammdatenException>(() =>
            new SicherungsarchivErstellungHttpClient(http).Erstellen(new SicherungsarchivErstellungsanfrage(Guid.Empty), default));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SicherungsarchivErstellungHttpClient(http).Erstellen(new SicherungsarchivErstellungsanfrage(OperationId), cts.Token));
        Assert.Equal(0, handler.Sends);
    }

    [Fact]
    public async Task PropagatesInFlightCancellationInstanceWithoutRetry()
    {
        var cancellation = new OperationCanceledException("cancelled");
        var handler = new FakeHandler(_ => throw cancellation);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var actual = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SicherungsarchivErstellungHttpClient(http).Erstellen(new SicherungsarchivErstellungsanfrage(OperationId), CancellationToken.None));
        Assert.Same(cancellation, actual);
        Assert.Equal(1, handler.Sends);
    }

    [Fact]
    public async Task PropagatesTransportFailureWithoutRetry()
    {
        var failure = new HttpRequestException("transport");
        var handler = new FakeHandler(_ => throw failure);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var actual = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new SicherungsarchivErstellungHttpClient(http).Erstellen(new SicherungsarchivErstellungsanfrage(OperationId), default));
        Assert.Same(failure, actual);
        Assert.Equal(1, handler.Sends);
    }

    private static HttpResponseMessage JsonResponse(SicherungsarchivErstellungsantwort answer) =>
        new(HttpStatusCode.OK) { Content = new TrackedContent(JsonSerializer.Serialize(answer)) };

    private static HttpResponseMessage RawResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new TrackedContent(json) };

    private sealed class FakeHandler(params Func<CancellationToken, Task<HttpResponseMessage>>[] replies) : HttpMessageHandler
    {
        private int _index;
        public List<(string Method, string Path, string? Body)> Requests { get; } = [];
        public List<TrackedContent> Contents { get; } = [];
        public List<HttpContent> RequestContents { get; } = [];
        public int Sends { get; private set; }
        public bool Disposed { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sends++;
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (request.Content is not null) RequestContents.Add(request.Content);
            Requests.Add((request.Method.Method, request.RequestUri!.PathAndQuery, body));
            var response = await replies[Math.Min(_index++, replies.Length - 1)](cancellationToken);
            if (response.Content is TrackedContent tracked) Contents.Add(tracked);
            return response;
        }

        protected override void Dispose(bool disposing) { if (disposing) Disposed = true; base.Dispose(disposing); }
    }

    private sealed class TrackedContent(string value) : StringContent(value, Encoding.UTF8, "application/json")
    {
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing) { if (disposing) Disposed = true; base.Dispose(disposing); }
    }

    private sealed class UnreadableContent(string value) : HttpContent
    {
        public bool Read { get; private set; }
        public bool Disposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context, CancellationToken cancellationToken)
        {
            Read = true;
            return stream.WriteAsync(Encoding.UTF8.GetBytes(value), cancellationToken).AsTask();
        }
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { if (disposing) Disposed = true; base.Dispose(disposing); }
    }
}
