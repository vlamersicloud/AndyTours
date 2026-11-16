using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Touren;
using AndyTours.Client;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;

namespace AndyTours.Tests.Import;

public sealed partial class TourenMehrfachHttpClientTests : IDisposable
{
    private readonly List<HttpClient> _ownedHttpClients = [];
    [Theory]
    [InlineData(400)]
    [InlineData(413)]
    public async Task Client_errors_400_and_413_map_to_input_rejection_without_reading_body(int status)
    {
        var (client, handler) = MakeClient(_ => new HttpResponseMessage((HttpStatusCode)status) { Content = new ThrowingContent() });
        await Assert.ThrowsAsync<UngueltigeTourException>(() => Send(client));
        Assert.Equal(1, handler.Sends);
        Assert.True(handler.ErrorContent!.Disposed);
        Assert.False(handler.ErrorContent.ReadAttempted);
    }

    [Theory]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(412)]
    [InlineData(500)]
    public async Task Other_http_errors_are_typed_without_reading_body(int status)
    {
        var (client, handler) = MakeClient(_ => new HttpResponseMessage((HttpStatusCode)status) { Content = new ThrowingContent() });
        if (status == 404) await Assert.ThrowsAsync<TourNichtGefundenException>(() => Send(client));
        else if (status is 409 or 412) await Assert.ThrowsAsync<TourVersionskonfliktException>(() => Send(client));
        else await Assert.ThrowsAsync<BackendApiException>(() => Send(client));
        Assert.Equal(1, handler.Sends);
        Assert.True(handler.ErrorContent!.Disposed);
        Assert.False(handler.ErrorContent.ReadAttempted);
    }

    [Fact]
    public async Task Cancellation_during_first_send_is_not_retried()
    {
        using var cts = new CancellationTokenSource();
        var (client, handler) = MakeClient(async token =>
        {
            await cts.CancelAsync();
            token.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Send(client, cts.Token));
        Assert.Equal(1, handler.Sends);
    }

    [Theory]
    [InlineData("wrong-id")]
    [InlineData("missing-id")]
    [InlineData("duplicate-id")]
    [InlineData("mixed")]
    [InlineData("bad-version")]
    [InlineData("null-values")]
    [InlineData("invalid-json")]
    public async Task Invalid_success_ack_is_uncertain_protocol_failure_and_never_echoes_payload(string shape)
    {
        var requestId = Guid.NewGuid();
        var tour = new TourVersionsreferenz(1, new string('A', 64));
        var values = TourenMapper.ToAntwort(new Trainingseinheit
        {
            Datum = new DateOnly(2026, 1, 1), Name = "Valid", AktivitaetId = 1, FahrerId = 1
        }).Werte;
        var responseJson = shape switch
        {
            "wrong-id" => JsonSerializer.Serialize(new TourenMehrfachAntwort(Guid.NewGuid(), [new(1, new string('B', 64), values)], [])),
            "missing-id" => JsonSerializer.Serialize(new TourenMehrfachAntwort(requestId, [], [])),
            "duplicate-id" => JsonSerializer.Serialize(new TourenMehrfachAntwort(requestId, [new(1, new string('B', 64), values), new(1, new string('C', 64), values)], [])),
            "mixed" => JsonSerializer.Serialize(new TourenMehrfachAntwort(requestId, [new(1, new string('B', 64), values)], [1])),
            "bad-version" => JsonSerializer.Serialize(new TourenMehrfachAntwort(requestId, [new(1, "bad", values)], [])),
            "null-values" => "{\"anfrageId\":\"" + requestId + "\",\"eintraege\":[{\"id\":1,\"version\":\"" + new string('B', 64) + "\",\"werte\":null}],\"geloeschteIds\":[]}",
            _ => "{SECRET_PAYLOAD"
        };
        var (client, handler) = MakeClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.Kategorien(new TourenKategorienAnfrage(requestId, [tour], [], []), default));
        if (shape == "invalid-json") Assert.DoesNotContain("SECRET_PAYLOAD", exception.Message);
        Assert.Equal(1, handler.Sends);
    }

    private static Task<TourenMehrfachAntwort> Send(TourenMehrfachHttpClient client, CancellationToken ct = default) =>
        client.Kategorien(new TourenKategorienAnfrage(Guid.NewGuid(),
            [new(1, new string('A', 64))], [], []), ct);

    private (TourenMehrfachHttpClient Client, FakeHandler Handler) MakeClient(
        Func<CancellationToken, Task<HttpResponseMessage>> respond)
    {
        var handler = new FakeHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        _ownedHttpClients.Add(http);
        return (new TourenMehrfachHttpClient(http), handler);
    }

    private (TourenMehrfachHttpClient Client, FakeHandler Handler) MakeClient(
        Func<CancellationToken, HttpResponseMessage> respond) =>
        MakeClient(ct => Task.FromResult(respond(ct)));

    private sealed class FakeHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Sends { get; private set; }
        public ThrowingContent? ErrorContent { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sends++;
            var response = await respond(cancellationToken);
            if (response.Content is ThrowingContent content) ErrorContent = content;
            return response;
        }
    }

    public void Dispose()
    {
        foreach (var http in _ownedHttpClients) http.Dispose();
        _ownedHttpClients.Clear();
    }

    private sealed class ThrowingContent : HttpContent
    {
        public bool ReadAttempted { get; private set; }
        public bool Disposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) { ReadAttempted = true; throw new Xunit.Sdk.XunitException("Error body was read."); }
        protected override Task<Stream> CreateContentReadStreamAsync() { ReadAttempted = true; throw new Xunit.Sdk.XunitException("Error body was read."); }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { if (disposing) Disposed = true; base.Dispose(disposing); }
    }
}
