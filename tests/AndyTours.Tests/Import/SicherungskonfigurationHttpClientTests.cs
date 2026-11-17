namespace AndyTours.Tests.Import;

using System.Net;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Client;
using AndyTours.Contracts.V1;
using Xunit;

public sealed class SicherungskonfigurationHttpClientTests
{
    private const string Version = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Get_and_put_use_exact_route_typed_payload_and_reusable_caller_client()
    {
        var handler = new RecordingHandler((request, _) => Task.FromResult(Ok(request.Method == HttpMethod.Get ? new(Version, 1, 99, 8) : new(Version, 99, 1, 8))));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new SicherungskonfigurationHttpClient(http);
        var loaded = await client.Laden(default);
        Assert.Equal(1, loaded.Intervall);
        Assert.Equal(99, loaded.Hoechstzahl);
        Assert.Equal(8, loaded.StartsSeitSicherung);
        var saved = await client.Speichern(new(Version, 99, 1), default);
        Assert.Equal(99, saved.Intervall);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(("GET", "/api/v1/einstellungen/sicherung/", (string?)null), handler.Requests[0]);
        Assert.Equal("PUT", handler.Requests[1].Method);
        Assert.Equal("/api/v1/einstellungen/sicherung/", handler.Requests[1].Path);
        using var json = JsonDocument.Parse(handler.Requests[1].Body!);
        var root = json.RootElement;
        Assert.Equal(3, root.EnumerateObject().Count());
        Assert.Equal(Version, root.GetProperty("version").GetString());
        Assert.Equal(99, root.GetProperty("intervall").GetInt32());
        Assert.Equal(1, root.GetProperty("hoechstzahl").GetInt32());
        Assert.False(root.TryGetProperty("startsSeitSicherung", out _));
        Assert.False(root.TryGetProperty("sicherungsordner", out _));
        Assert.Equal(2, handler.Responses.Count);
        Assert.All(handler.Responses, response => Assert.True(response.Disposed));
        Assert.Equal(2, handler.Sends);
        Assert.False(handler.Disposed);
    }

    [Theory]
    [InlineData(400, StammdatenFehlerart.Ungueltig, "Sicherungskonfiguration ungültig.")]
    [InlineData(409, StammdatenFehlerart.Konflikt, "Sicherungskonfiguration wurde zwischenzeitlich geändert.")]
    public async Task Maps_safe_errors_without_reading_body(int status, StammdatenFehlerart kind, string message)
    {
        var content = new UnreadableTrackedContent("BODY-MUST-NOT-APPEAR");
        using var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = content }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var error = await Assert.ThrowsAsync<StammdatenException>(() => new SicherungskonfigurationHttpClient(http).Laden(default));
        Assert.Equal(kind, error.Art);
        Assert.Equal(message, error.Message);
        Assert.False(content.Read);
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task Other_status_preserves_status_but_not_server_body()
    {
        var content = new UnreadableTrackedContent("SENSITIVE-SERVER-BODY");
        using var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = content }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => new SicherungskonfigurationHttpClient(http).Laden(default));
        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        Assert.DoesNotContain("SENSITIVE-SERVER-BODY", error.Message, StringComparison.Ordinal);
        Assert.False(content.Read);
        Assert.True(content.Disposed);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"version\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\"intervall\":3,\"hoechstzahl\":10}")]
    [InlineData("{\"version\":\"bad\",\"intervall\":3,\"hoechstzahl\":10,\"startsSeitSicherung\":0}")]
    [InlineData("{\"version\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\"intervall\":0,\"hoechstzahl\":10,\"startsSeitSicherung\":0}")]
    [InlineData("{\"version\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\"intervall\":3,\"hoechstzahl\":100,\"startsSeitSicherung\":0}")]
    [InlineData("{\"version\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\"intervall\":3,\"hoechstzahl\":10,\"startsSeitSicherung\":-1}")]
    public async Task Rejects_invalid_responses_without_echoing_payload(string payload)
    {
        var content = new TrackedContent(payload);
        using var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => new SicherungskonfigurationHttpClient(http).Laden(default));
        Assert.Contains("ungültig", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(payload, error.Message, StringComparison.Ordinal);
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task Invalid_request_is_rejected_before_send_and_cancellation_is_not_retried()
    {
        using var cts = new CancellationTokenSource();
        using var handler = new RecordingHandler((_, ct) =>
        {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Ok(new(Version, 3, 10, 0)));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new SicherungskonfigurationHttpClient(http);
        await Assert.ThrowsAsync<StammdatenException>(() => client.Speichern(new("bad", 3, 10), default));
        Assert.Equal(0, handler.Sends);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Laden(cts.Token));
        Assert.Equal(1, handler.Sends);
    }

    private static HttpResponseMessage Ok(SicherungskonfigurationAntwort answer) =>
        new(HttpStatusCode.OK) { Content = new TrackedContent(JsonSerializer.Serialize(answer)) };

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public List<(string Method, string Path, string? Body)> Requests { get; } = [];
        public List<TrackedContent> Responses { get; } = [];
        public int Sends { get; private set; }
        public bool Disposed { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sends++;
            string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method.Method, request.RequestUri!.AbsolutePath, body));
            var response = await send(request, cancellationToken);
            if (response.Content is TrackedContent tracked) Responses.Add(tracked);
            return response;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class UnreadableTrackedContent(string text) : HttpContent
    {
        public bool Read { get; private set; }
        public bool Disposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            Read = true;
            return stream.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken).AsTask();
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => SerializeToStreamAsync(stream, context, CancellationToken.None);
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { if (disposing) Disposed = true; base.Dispose(disposing); }
    }

    private sealed class TrackedContent(string text) : StringContent(text, Encoding.UTF8, "application/json")
    {
        public bool Read { get; private set; }
        public bool Disposed { get; private set; }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            Read = true;
            await base.SerializeToStreamAsync(stream, context);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Disposed = true;
            base.Dispose(disposing);
        }
    }
}
