namespace AndyTours.Tests.Sicherung;

using System.Net;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Client;
using Xunit;

public sealed class SqliteBasispruefungHttpClientTests
{
    private static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly string Hash = new('a', 64);
    private static string OkBody => $$"""{"sicherungsId":"{{Id}}","status":"ok","schemaVersion":1,"coreTablesChecked":8,"quickCheck":true,"hinweise":[]}""";
    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://localhost/") };

    [Fact]
    public async Task ExactPostAndCallerClientOwnership()
    {
        var handler = new Handler(HttpStatusCode.OK, OkBody); using var http = Client(handler); var port = new SqliteBasispruefungHttpClient(http);
        var answer = await port.Pruefen(new(Id, Hash), CancellationToken.None);
        Assert.Equal("ok", answer.Status); Assert.Equal("/api/v1/sicherungen/sqlite-basispruefung", handler.Path);
        using (var json = JsonDocument.Parse(handler.Body!))
        { Assert.Equal(Id, json.RootElement.GetProperty("sicherungsId").GetGuid()); Assert.Equal(Hash, json.RootElement.GetProperty("erwarteterInhaltHash").GetString()); Assert.Equal(2, json.RootElement.EnumerateObject().Count()); }
        handler.Set(HttpStatusCode.OK, new TrackedContent(OkBody));
        Assert.Equal("ok", (await port.Pruefen(new(Id, Hash), CancellationToken.None)).Status);
        Assert.True(handler.LastContent!.Disposed);
    }

    [Theory]
    [InlineData(400, StammdatenFehlerart.Ungueltig)] [InlineData(404, StammdatenFehlerart.NichtGefunden)] [InlineData(409, StammdatenFehlerart.Konflikt)]
    public async Task ErrorBodiesAreUnreadAndDisposed(int code, StammdatenFehlerart kind)
    {
        var content = new TrackedContent("private details"); var handler = new Handler((HttpStatusCode)code, content);
        using var http = Client(handler); var ex = await Assert.ThrowsAsync<StammdatenException>(() => new SqliteBasispruefungHttpClient(http).Pruefen(new(Id, Hash), CancellationToken.None));
        Assert.Equal(kind, ex.Art); Assert.False(content.Read); Assert.True(content.Disposed);
    }
    [Fact]
    public async Task UnavailableBodyUnreadAndPreCancellationDoesNotSend()
    {
        var content = new TrackedContent("private details"); var handler = new Handler(HttpStatusCode.ServiceUnavailable, content);
        using var http = Client(handler); var ex = await Assert.ThrowsAsync<HttpRequestException>(() => new SqliteBasispruefungHttpClient(http).Pruefen(new(Id, Hash), CancellationToken.None));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode); Assert.False(content.Read); Assert.True(content.Disposed);
        var second = new Handler(HttpStatusCode.OK, OkBody); using var secondHttp = Client(second); using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SqliteBasispruefungHttpClient(secondHttp).Pruefen(new(Id, Hash), cts.Token)); Assert.Null(second.Path);
    }
    [Theory]
    [InlineData("null")]
    [InlineData("{\"sicherungsId\":\"00000000-0000-0000-0000-000000000001\",\"status\":\"unknown\",\"schemaVersion\":1,\"coreTablesChecked\":8,\"quickCheck\":true,\"hinweise\":[]}")]
    [InlineData("{\"sicherungsId\":\"00000000-0000-0000-0000-000000000001\",\"status\":\"ok\",\"schemaVersion\":1,\"coreTablesChecked\":7,\"quickCheck\":true,\"hinweise\":[]}")]
    [InlineData("{\"sicherungsId\":\"00000000-0000-0000-0000-000000000001\",\"status\":\"ok\",\"schemaVersion\":null,\"coreTablesChecked\":8,\"quickCheck\":true,\"hinweise\":[]}")]
    [InlineData("{\"sicherungsId\":\"00000000-0000-0000-0000-000000000001\",\"status\":\"invalid_input\",\"schemaVersion\":null,\"coreTablesChecked\":0,\"quickCheck\":false,\"hinweise\":null}")]
    public async Task MalformedResponsesRejected(string body)
    { using var http = Client(new Handler(HttpStatusCode.OK, body)); await Assert.ThrowsAsync<HttpRequestException>(() => new SqliteBasispruefungHttpClient(http).Pruefen(new(Id, Hash), CancellationToken.None)); }

    private sealed class Handler(HttpStatusCode status, object content) : HttpMessageHandler
    {
        private HttpStatusCode _status = status; private object _content = content;
        public string? Path; public string? Body; public TrackedContent? LastContent;
        public void Set(HttpStatusCode next, TrackedContent body) { _status = next; _content = body; LastContent = body; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath; Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            HttpContent content = _content switch { string s => new StringContent(s, Encoding.UTF8, "application/json"), TrackedContent t => t, _ => throw new InvalidOperationException() };
            if (content is TrackedContent tracked) LastContent = tracked;
            return new HttpResponseMessage(_status) { Content = content };
        }
    }
    private sealed class TrackedContent(string value) : HttpContent
    {
        public bool Read; public bool Disposed;
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context) { Read = true; await stream.WriteAsync(Encoding.UTF8.GetBytes(value)); }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
