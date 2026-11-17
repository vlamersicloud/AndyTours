namespace AndyTours.Tests.Import;

using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Client;
using AndyTours.Contracts.V1;
using Xunit;

public sealed class WiederherstellungsvorschauHttpClientTests
{
    private static readonly Guid FullId = Guid.Parse("4e1f6d1c-1284-4a7e-a95b-2e7b6215ed10");
    private static readonly DateTimeOffset FullUtc = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private const string HashLower = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string HashUpper = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
    private const string Route = "/api/v1/sicherungen/wiederherstellungsvorschau/";

    [Fact]
    public async Task VollarchivRequestUndAntwortSindExaktUndReadonly()
    {
        var handler = new FakeHandler(() => JsonResponse(FullAntwort()), () => JsonResponse(FullAntwort()));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new WiederherstellungsvorschauHttpClient(http);
        var result = await client.Pruefen(new WiederherstellungsvorschauAnfrage(FullId, HashLower), default);
        var second = await client.Pruefen(new WiederherstellungsvorschauAnfrage(FullId, HashLower), default);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(Route, handler.Route);
        using var requestDoc = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(2, requestDoc.RootElement.EnumerateObject().Count());
        Assert.Equal(FullId, requestDoc.RootElement.GetProperty("sicherungsId").GetGuid());
        Assert.Equal(HashUpper, requestDoc.RootElement.GetProperty("erwarteterInhaltHash").GetString());
        Assert.Null(result.ManifestFormatversion);
        Assert.Null(result.Dateianzahl);
        Assert.Null(result.NutzdatenBytes);
        Assert.False(result.DatenbankGeprueft);
        Assert.False(result.AnwendungFreigegeben);
        Assert.True(((IList<string>)result.Hinweise).IsReadOnly);
        Assert.Equal(2, handler.SendCount);
        Assert.Equal(2, handler.RequestContents.Count);
        foreach (var content in handler.RequestContents)
            await Assert.ThrowsAsync<ObjectDisposedException>(() => content.ReadAsStringAsync());
        Assert.Equal(2, handler.Contents.Count);
        Assert.All(handler.Contents, content => Assert.True(((TrackedContent)content).Disposed));
        Assert.Equal(result.Sicherung, second.Sicherung);
        Assert.Equal(result.Hinweise.ToArray(), second.Hinweise.ToArray());
        Assert.False(handler.Disposed);
    }

    [Fact]
    public async Task LegacyAntwortBewahrtBekannteZaehlerOhneFreigabe()
    {
        var response = new WiederherstellungsvorschauAntwort(
            new SicherungAntwort(FullId, Sicherungsart.LegacySqlite, null, 432, HashUpper)
                { LegacyZeitstempel = "2024-02-29_12-34-56" },
            null, 1, 432, false, false, ["Legacy enthält nur die Datenbank."]);
        using var http = new HttpClient(new FakeHandler(() => JsonResponse(response))) { BaseAddress = new Uri("https://example.test/") };
        var result = await new WiederherstellungsvorschauHttpClient(http).Pruefen(
            new WiederherstellungsvorschauAnfrage(FullId, HashLower), default);
        Assert.Equal(1, result.Dateianzahl);
        Assert.Equal(432L, result.NutzdatenBytes);
        Assert.Null(result.ManifestFormatversion);
        Assert.False(result.AnwendungFreigegeben);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, StammdatenFehlerart.Ungueltig)]
    [InlineData(HttpStatusCode.NotFound, StammdatenFehlerart.NichtGefunden)]
    [InlineData(HttpStatusCode.Conflict, StammdatenFehlerart.Konflikt)]
    public async Task FachstatusWerdenSicherAbgebildet(HttpStatusCode status, StammdatenFehlerart art)
    {
        var handler = new FakeHandler(() => new HttpResponseMessage(status) { Content = ErrorContent("PRIVATE error body") });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var ex = await Assert.ThrowsAsync<StammdatenException>(() =>
            new WiederherstellungsvorschauHttpClient(http).Pruefen(new WiederherstellungsvorschauAnfrage(FullId, HashLower), default));
        Assert.Equal(art, ex.Art);
        Assert.DoesNotContain("PRIVATE", ex.Message);
        Assert.False(((UnreadableContent)handler.Contents[0]).Read);
        Assert.True(((UnreadableContent)handler.Contents[0]).Disposed);
        Assert.Equal(1, handler.SendCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ServerstatusSindSicherUndOhneBodylesung(HttpStatusCode status)
    {
        var handler = new FakeHandler(() => new HttpResponseMessage(status) { Content = ErrorContent("PRIVATE error body") });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new WiederherstellungsvorschauHttpClient(http).Pruefen(new WiederherstellungsvorschauAnfrage(FullId, HashLower), default));
        Assert.Equal(status, ex.StatusCode);
        Assert.DoesNotContain("PRIVATE", ex.Message);
        Assert.False(((UnreadableContent)handler.Contents[0]).Read);
        Assert.True(((UnreadableContent)handler.Contents[0]).Disposed);
        Assert.Equal(1, handler.SendCount);
    }

    [Theory]
    [MemberData(nameof(UngueltigeAntworten))]
    public async Task UngueltigeAntwortenWerdenSicherVerworfen(WiederherstellungsvorschauAntwort antwort)
    {
        using var http = new HttpClient(new FakeHandler(() => JsonResponse(antwort))) { BaseAddress = new Uri("https://example.test/") };
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new WiederherstellungsvorschauHttpClient(http).Pruefen(new WiederherstellungsvorschauAnfrage(FullId, HashLower), default));
        Assert.Equal("Wiederherstellungsvorschau-Antwort ungültig.", ex.Message);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task MalformedJsonWirdSicherAbgelehnt(string payload)
    {
        using var http = new HttpClient(new FakeHandler(() => RawResponse(payload))) { BaseAddress = new Uri("https://example.test/") };
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new WiederherstellungsvorschauHttpClient(http).Pruefen(new WiederherstellungsvorschauAnfrage(FullId, HashLower), default));
        Assert.Equal("Wiederherstellungsvorschau-Antwort ungültig.", ex.Message);
        Assert.DoesNotContain(payload, ex.Message);
    }

    [Fact]
    public async Task FehlendesErforderlichesFeldWirdAbgelehnt()
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(FullAntwort(), JsonOptions))!.AsObject();
        json.Remove("anwendungFreigegeben");
        using var http = new HttpClient(new FakeHandler(() => RawResponse(json.ToJsonString()))) { BaseAddress = new Uri("https://example.test/") };
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new WiederherstellungsvorschauHttpClient(http).Pruefen(new WiederherstellungsvorschauAnfrage(FullId, HashLower), default));
    }

    [Fact]
    public async Task UngueltigeAnfrageUndVorabAbbruchSendenNicht()
    {
        var handler = new FakeHandler(() => JsonResponse(FullAntwort()));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new WiederherstellungsvorschauHttpClient(http);
        await Assert.ThrowsAsync<AndyTours.Application.Stammdaten.StammdatenException>(() =>
            client.Pruefen(new WiederherstellungsvorschauAnfrage(Guid.Empty, HashLower), default));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Pruefen(new WiederherstellungsvorschauAnfrage(FullId, HashLower), cts.Token));
        Assert.Equal(0, handler.SendCount);
    }

    [Fact]
    public async Task TransportfehlerWerdenUnveraendertUndOhneRetryWeitergegeben()
    {
        var failure = new HttpRequestException("transport");
        var handler = new FakeHandler(() => throw failure);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new WiederherstellungsvorschauHttpClient(http).Pruefen(new WiederherstellungsvorschauAnfrage(FullId, HashLower), default));
        Assert.Same(failure, thrown);
        Assert.Equal(1, handler.SendCount);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    [Fact]
    public async Task InFlightCancellationWirdNichtUmgewandeltOderWiederholt()
    {
        var cancellation = new OperationCanceledException("cancelled");
        var handler = new FakeHandler(() => throw cancellation);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new WiederherstellungsvorschauHttpClient(http).Pruefen(new WiederherstellungsvorschauAnfrage(FullId, HashLower), CancellationToken.None));
        Assert.Same(cancellation, thrown);
        Assert.Equal(1, handler.SendCount);
    }

    public static IEnumerable<object[]> UngueltigeAntworten()
    {
        var valid = FullAntwort();
        yield return [valid with { Sicherung = valid.Sicherung with { Id = Guid.NewGuid() } }];
        yield return [valid with { Sicherung = valid.Sicherung with { InhaltHash = new string('0', 64) } }];
        yield return [valid with { AnwendungFreigegeben = true }];
        yield return [valid with { DatenbankGeprueft = true }];
        yield return [valid with { Dateianzahl = 0 }];
        yield return [valid with { Hinweise = Array.Empty<string>() }];
        yield return [valid with { Hinweise = [""] }];
        yield return [valid with { Sicherung = valid.Sicherung with { Byteanzahl = -1 } }];
        yield return [valid with { Sicherung = valid.Sicherung with { ErstelltUtc = FullUtc.ToOffset(TimeSpan.FromHours(1)) } }];
        yield return [valid with { Sicherung = valid.Sicherung with { LegacyZeitstempel = "2024-01-01_00-00-00" } }];
    }

    private static WiederherstellungsvorschauAntwort FullAntwort() => new(
        new SicherungAntwort(FullId, Sicherungsart.Vollarchiv, FullUtc, 123, HashUpper),
        null, null, null, false, false, ["Metadatenvorschau; Datenbank ungeprüft."]);

    private static HttpResponseMessage JsonResponse(WiederherstellungsvorschauAntwort answer) => new(HttpStatusCode.OK)
    {
        Content = new TrackedContent(JsonSerializer.Serialize(answer, JsonOptions), Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage RawResponse(string value) => new(HttpStatusCode.OK) { Content = new TrackedContent(value, Encoding.UTF8, "application/json") };

    private static UnreadableContent ErrorContent(string value) => new(value);

    private sealed class FakeHandler(params Func<HttpResponseMessage>[] replies) : HttpMessageHandler
    {
        private int _index;
        public int SendCount { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? Route { get; private set; }
        public string? RequestBody { get; private set; }
        public List<HttpContent> Contents { get; } = [];
        public List<HttpContent> RequestContents { get; } = [];
        public bool Disposed { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            Method = request.Method;
            Route = request.RequestUri?.PathAndQuery;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            RequestContents.Add(request.Content);
            var response = replies[Math.Min(_index++, replies.Length - 1)]();
            Contents.Add(response.Content);
            return response;
        }

        protected override void Dispose(bool disposing) { if (disposing) Disposed = true; base.Dispose(disposing); }
    }

    private sealed class TrackedContent(string value, Encoding? encoding = null, string? mediaType = null) : StringContent(value, encoding ?? Encoding.UTF8, mediaType ?? "application/json")
    {
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing) { if (disposing) Disposed = true; base.Dispose(disposing); }
    }

    private sealed class UnreadableContent(string value) : HttpContent
    {
        public bool Read { get; private set; }
        public bool Disposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            Read = true;
            return stream.WriteAsync(Encoding.UTF8.GetBytes(value)).AsTask();
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { if (disposing) Disposed = true; base.Dispose(disposing); }
    }
}
