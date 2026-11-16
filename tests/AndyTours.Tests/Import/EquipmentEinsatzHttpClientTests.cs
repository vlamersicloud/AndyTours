using System.Net;
using System.Text;
using AndyTours.Application.Touren;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Import;

public sealed class EquipmentEinsatzHttpClientTests
{
    [Fact]
    public async Task Sends_exact_readonly_get_and_returns_a_defensive_readonly_projection()
    {
        const string json = """{"zeilen":[{"name":"Bike","vorherKm":null,"gesamtKm":442.65,"gesamtHm":null,"istSumme":false},{"name":"Summe","vorherKm":null,"gesamtKm":442.65,"gesamtHm":71,"istSumme":true}]}""";
        var contents = new List<TrackingContent>();
        var handler = new RecordingHandler(_ =>
        {
            var content = new TrackingContent(json);
            contents.Add(content);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new EquipmentEinsatzHttpClient(http);

        var result = await client.Laden();

        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("api/v1/equipment-einsatz", handler.Path);
        Assert.Equal(string.Empty, handler.Query);
        Assert.Null(handler.RequestBody);
        Assert.Equal(2, result.Zeilen.Count);
        Assert.Null(result.Zeilen[0].VorherKm);
        Assert.Equal(442.65, result.Zeilen[0].GesamtKm);
        Assert.Equal(71, result.Zeilen[^1].GesamtHm);
        Assert.True(result.Zeilen[^1].IstSumme);
        Assert.True(((IList<EquipmentEinsatzzeileAntwort>)result.Zeilen).IsReadOnly);
        Assert.Single(contents);
        Assert.All(contents, content => Assert.True(content.Disposed));
        await client.Laden();
        Assert.Equal(2, handler.Calls);
        Assert.Equal(2, contents.Count);
        Assert.All(contents, content => Assert.True(content.Disposed));
    }

    [Fact]
    public async Task Empty_projection_is_valid_and_response_content_is_disposed()
    {
        var content = new TrackingContent("{\"zeilen\":[]}");
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        Assert.Empty((await new EquipmentEinsatzHttpClient(http).Laden()).Zeilen);
        Assert.True(content.Disposed);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Error_status_never_reads_body_and_does_not_echo_it(HttpStatusCode status)
    {
        var content = new ThrowOnReadContent("secret-sentinel");
        var handler = new RecordingHandler(_ => new HttpResponseMessage(status) { Content = content });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        if (status == HttpStatusCode.BadRequest)
        {
            var ex = await Assert.ThrowsAsync<UngueltigeTourException>(() => new EquipmentEinsatzHttpClient(http).Laden());
            Assert.DoesNotContain("secret-sentinel", ex.Message);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => new EquipmentEinsatzHttpClient(http).Laden());
            Assert.DoesNotContain("secret-sentinel", ex.Message);
        }
        Assert.False(content.ReadAttempted);
        Assert.True(content.Disposed);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("secret-sentinel")]
    [InlineData("null")]
    [InlineData("{\"zeilen\":null}")]
    [InlineData("{\"zeilen\":[null]}")]
    [InlineData("{\"zeilen\":[{\"name\":null,\"vorherKm\":null,\"gesamtKm\":1,\"gesamtHm\":null,\"istSumme\":true}]}")]
    [InlineData("{\"zeilen\":[{\"name\":\"sum\",\"vorherKm\":null,\"gesamtKm\":1,\"gesamtHm\":null,\"istSumme\":true},{\"name\":\"later\",\"vorherKm\":null,\"gesamtKm\":1,\"gesamtHm\":null,\"istSumme\":false}]}")]
    [InlineData("{\"zeilen\":[{\"name\":\"one\",\"vorherKm\":null,\"gesamtKm\":1,\"gesamtHm\":null,\"istSumme\":false}]}")]
    [InlineData("{\"zeilen\":[{\"name\":\"one\",\"vorherKm\":null,\"gesamtKm\":1,\"gesamtHm\":null,\"istSumme\":true},{\"name\":\"two\",\"vorherKm\":null,\"gesamtKm\":2,\"gesamtHm\":null,\"istSumme\":true}]}")]
    [InlineData("{\"zeilen\":[{\"name\":\"bad\",\"vorherKm\":null,\"gesamtKm\":1e999,\"gesamtHm\":null,\"istSumme\":true}]}")]
    public async Task Invalid_response_never_becomes_a_projection(string json)
    {
        var handler = new RecordingHandler(_ => JsonResponse(json));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => new EquipmentEinsatzHttpClient(http).Laden());
        Assert.DoesNotContain("secret-sentinel", ex.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Pre_cancelled_request_does_not_send()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{\"zeilen\":[]}"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new EquipmentEinsatzHttpClient(http).Laden(cts.Token));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Cancellation_during_first_send_propagates_without_retry()
    {
        using var cts = new CancellationTokenSource();
        var handler = new RecordingHandler(_ => { cts.Cancel(); cts.Token.ThrowIfCancellationRequested(); return JsonResponse("{\"zeilen\":[]}"); });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new EquipmentEinsatzHttpClient(http).Laden(cts.Token));
        Assert.Equal(1, handler.Calls);
    }

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Query { get; private set; }
        public string? RequestBody { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath.TrimStart('/');
            Query = request.RequestUri?.Query;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return respond(request);
        }
    }

    private sealed class TrackingContent(string body) : StringContent(body, Encoding.UTF8, "application/json")
    {
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class ThrowOnReadContent(string sentinel) : HttpContent
    {
        public bool ReadAttempted { get; private set; }
        public bool Disposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            ReadAttempted = true; throw new InvalidOperationException(sentinel);
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
