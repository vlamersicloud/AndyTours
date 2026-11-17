namespace AndyTours.Tests.Import;

using System.Net;
using System.Text;
using System.Text.Json;
using AndyTours.Client;
using AndyTours.Contracts.V1;
using Xunit;

public sealed class SicherungskatalogHttpClientTests
{
    private const string Hash = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    [Fact]
    public async Task Gets_exact_path_and_deserializes_both_catalog_entry_kinds_reusing_caller_client()
    {
        var legacy = new SicherungAntwort(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Sicherungsart.LegacySqlite, null, 4, Hash) { LegacyZeitstempel = "2024-02-29_12-34-56" };
        var archive = new SicherungAntwort(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Sicherungsart.Vollarchiv, new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero), 8, Hash);
        var handler = new FakeHandler(_ => Task.FromResult(Ok(new SicherungListeAntwort([legacy]))),
            _ => Task.FromResult(Ok(new SicherungListeAntwort([archive]))));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new SicherungskatalogHttpClient(http);
        var first = await client.Liste(default);
        var second = await client.Liste(default);
        var actualLegacy = Assert.Single(first.Eintraege);
        Assert.Equal(legacy, actualLegacy);
        Assert.Null(actualLegacy.ErstelltUtc);
        Assert.Equal("2024-02-29_12-34-56", actualLegacy.LegacyZeitstempel);
        Assert.Equal(archive, Assert.Single(second.Eintraege));
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal(("GET", "/api/v1/sicherungen/", (string?)null), request));
        Assert.All(handler.Contents, content => Assert.True(content.Disposed));
        Assert.Equal(2, handler.Sends);
        Assert.False(handler.Disposed);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("""{"eintraege":null}""")]
    [InlineData("{\"eintraege\":[null]}")]
    [InlineData("{\"eintraege\":[{\"id\":\"00000000-0000-0000-0000-000000000000\",\"art\":0,\"erstelltUtc\":null,\"byteanzahl\":1,\"inhaltHash\":\"0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF\",\"legacyZeitstempel\":\"2024-01-01_00-00-00\"}]}")]
    [InlineData("{\"eintraege\":[{\"id\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"art\":99,\"erstelltUtc\":null,\"byteanzahl\":1,\"inhaltHash\":\"0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF\"}]}")]
    public async Task Rejects_malformed_or_invalid_catalog_payloads(string payload)
    {
        var handler = new FakeHandler(_ => Task.FromResult(OkRaw(payload)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => new SicherungskatalogHttpClient(http).Liste(default));
        Assert.Contains("ungültig", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(payload, error.Message, StringComparison.Ordinal);
        Assert.True(Assert.Single(handler.Contents).Disposed);
    }

    [Fact]
    public async Task Maps_error_status_without_reading_or_echoing_body()
    {
        foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError })
        {
            var content = new UnreadableContent("PRIVATE-ERROR-BODY");
            var handler = new FakeHandler(_ => Task.FromResult(new HttpResponseMessage(status) { Content = content }));
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
            var error = await Assert.ThrowsAsync<HttpRequestException>(() => new SicherungskatalogHttpClient(http).Liste(default));
            Assert.Equal(status, error.StatusCode);
            Assert.DoesNotContain("PRIVATE-ERROR-BODY", error.Message, StringComparison.Ordinal);
            Assert.False(content.Read);
            Assert.True(content.Disposed);
            Assert.False(handler.Disposed);
        }
    }

    [Fact]
    public async Task Cancellation_is_not_retried()
    {
        using var cts = new CancellationTokenSource();
        var handler = new FakeHandler(_ => throw new OperationCanceledException());
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SicherungskatalogHttpClient(http).Liste(cts.Token));
        Assert.Equal(0, handler.Sends);
    }

    private static HttpResponseMessage Ok(SicherungListeAntwort value) =>
        new(HttpStatusCode.OK) { Content = new TrackedContent(JsonSerializer.Serialize(value)) };

    private static HttpResponseMessage OkRaw(string value) =>
        new(HttpStatusCode.OK) { Content = new TrackedContent(value) };

    private sealed class FakeHandler(params Func<CancellationToken, Task<HttpResponseMessage>>[] replies) : HttpMessageHandler
    {
        private int _index;
        public List<(string Method, string Path, string? Body)> Requests { get; } = [];
        public List<TrackedContent> Contents { get; } = [];
        public int Sends { get; private set; }
        public bool Disposed { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sends++;
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method.Method, request.RequestUri!.PathAndQuery, body));
            var response = await replies[_index++](cancellationToken);
            if (response.Content is TrackedContent tracked) Contents.Add(tracked);
            return response;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Disposed = true;
            base.Dispose(disposing);
        }
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
