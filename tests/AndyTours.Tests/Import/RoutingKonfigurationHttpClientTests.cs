using System.Net;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Import;

public sealed class RoutingKonfigurationHttpClientTests
{
    private const string Version = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task All_six_operations_use_expected_method_path_and_typed_payloads()
    {
        var calls = new List<(string Method, string Path, string Body)>();
        using var handler = new Handler(async (request, ct) =>
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            calls.Add((request.Method.Method, request.RequestUri!.AbsolutePath, body));
            return Ok(request.Method == HttpMethod.Get
                ? new(Version, RoutingAnbieter.BRouter, "https://router.example/brouter", false, true)
                : new(Version, RoutingAnbieter.Openrouteservice, null, true, true));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new RoutingKonfigurationHttpClient(http);
        await client.Laden(default);
        await client.DienstSpeichern(new(Version, RoutingAnbieter.Openrouteservice), default);
        await client.AdresseSpeichern(new(Version, "https://router.example/custom"), default);
        await client.AdresseZuruecksetzen(new(Version), default);
        await client.OrsKeySpeichern(new(Version, "FAKE-SECRET"), default);
        await client.OrsKeyEntfernen(new(Version), default);

        Assert.Equal(new[]
        {
            ("GET", "/api/v1/routing-konfiguration"),
            ("PUT", "/api/v1/routing-konfiguration/dienst"),
            ("PUT", "/api/v1/routing-konfiguration/brouter-adresse"),
            ("POST", "/api/v1/routing-konfiguration/brouter-adresse/zuruecksetzen"),
            ("PUT", "/api/v1/routing-konfiguration/ors-schluessel"),
            ("POST", "/api/v1/routing-konfiguration/ors-schluessel/entfernen")
        }, calls.Select(x => (x.Method, x.Path)));
        Assert.Contains(Version, calls[1].Body, StringComparison.Ordinal);
        Assert.Contains("openrouteservice", calls[1].Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FAKE-SECRET", calls[4].Body, StringComparison.Ordinal);
        Assert.DoesNotContain("FAKE-SECRET", string.Join("", calls.Where((_, i) => i != 4).Select(x => x.Body)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(409)]
    public async Task Safe_errors_do_not_read_response_body(int status)
    {
        var body = new TrackingContent("private provider configuration");
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = body }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        await Assert.ThrowsAsync<StammdatenException>(() => new RoutingKonfigurationHttpClient(http).Laden(default));
        Assert.False(body.Read);
        Assert.True(body.Disposed);
    }

    [Theory]
    [InlineData("{\"version\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\"dienst\":0,\"bRouterAdresse\":null,\"adresseAusgeblendet\":false,\"hatOrsSchluessel\":false}")]
    [InlineData("{\"version\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\"dienst\":0,\"bRouterAdresse\":\"https://u:p@router.invalid/x\",\"adresseAusgeblendet\":false,\"hatOrsSchluessel\":false}")]
    public async Task Rejects_malformed_or_credentialed_public_response(string json)
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        await Assert.ThrowsAsync<HttpRequestException>(() => new RoutingKonfigurationHttpClient(http).Laden(default));
    }

    [Fact]
    public async Task Invalid_request_is_rejected_before_send_and_cancellation_is_not_retried()
    {
        using var handler = new Handler((_, ct) => throw new OperationCanceledException(ct));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new RoutingKonfigurationHttpClient(http);
        await Assert.ThrowsAsync<StammdatenException>(() => client.OrsKeySpeichern(new("bad", "FAKE"), default));
        Assert.Equal(0, handler.Sends);
        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Laden(cts.Token));
        Assert.Equal(1, handler.Sends);
    }

    private static HttpResponseMessage Ok(RoutingKonfigurationAntwort answer) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(answer), Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Sends { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Sends++; return send(request, cancellationToken); }
    }

    private sealed class TrackingContent(string value) : HttpContent
    {
        public bool Read { get; private set; }
        public bool Disposed { get; private set; }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        { Read = true; await stream.WriteAsync(Encoding.UTF8.GetBytes(value)); }
        protected override bool TryComputeLength(out long length) { length = Encoding.UTF8.GetByteCount(value); return true; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
