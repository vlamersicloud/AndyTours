using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Touren;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Import;

public sealed class TourenSortierungHttpClientTests
{
    private const string Version = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task List_sends_finite_sort_and_all_filters_and_disposes_response(bool descending)
    {
        var content = new TrackingContent(JsonSerializer.Serialize(new TourListeAntwort([], 0, 4, 3)));
        HttpRequestMessage? captured = null;
        using var handler = new Handler((request, _) =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var filter = new TourenListenfilter(new DateOnly(2026, 2, 3), new DateOnly(2026, 9, 8), 17, 23)
        { Sortierung = TourenSortierschluessel.Hoehenmeter, Absteigend = descending };
        var result = await new TourenHttpClient(http).ListeGefiltert(4, 3, filter);
        Assert.Equal(0, result.Gesamt);
        Assert.True(content.Disposed);
        Assert.Equal(HttpMethod.Get, captured!.Method);
        var query = ParseQuery(captured.RequestUri!.Query);
        Assert.Equal("4", query["start"]);
        Assert.Equal("3", query["anzahl"]);
        Assert.Equal("2026-02-03", query["von"]);
        Assert.Equal("2026-09-08", query["bis"]);
        Assert.Equal("17", query["aktivitaetId"]);
        Assert.Equal("23", query["kategorieId"]);
        Assert.Equal(((int)TourenSortierschluessel.Hoehenmeter).ToString(CultureInfo.InvariantCulture), query["sortierung"]);
        Assert.Equal(descending ? "true" : "false", query["absteigend"]);
        Assert.Equal(1, handler.Sends);
    }

    [Fact]
    public async Task Default_filter_sends_existing_order_values_numerically()
    {
        Uri? uri = null;
        using var handler = new Handler((request, _) =>
        {
            uri = request.RequestUri;
            return Task.FromResult(Ok());
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        await new TourenHttpClient(http).ListeGefiltert(0, 50, new(null, null, null, null));
        var query = ParseQuery(uri!.Query);
        Assert.Equal("0", query["sortierung"]);
        Assert.Equal("false", query["absteigend"]);
    }

    [Fact]
    public async Task Invalid_key_and_precancellation_do_not_send()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Ok()));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var client = new TourenHttpClient(http);
        await Assert.ThrowsAsync<UngueltigeTourException>(() => client.ListeGefiltert(0, 5,
            new(null, null, null, null) { Sortierung = (TourenSortierschluessel)99 }));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ListeGefiltert(0, 5, new(null, null, null, null), cts.Token));
        Assert.Equal(0, handler.Sends);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(500)]
    public async Task Error_response_body_is_never_read(int status)
    {
        var body = new TrackingContent("sensitive server detail");
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = body }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        if (status == 400)
            await Assert.ThrowsAsync<UngueltigeTourException>(() => new TourenHttpClient(http).ListeGefiltert(0, 5, new(null, null, null, null)));
        else
            await Assert.ThrowsAsync<BackendApiException>(() => new TourenHttpClient(http).ListeGefiltert(0, 5, new(null, null, null, null)));
        Assert.False(body.Read);
        Assert.True(body.Disposed);
    }

    [Fact]
    public async Task Cancellation_during_first_send_is_not_retried()
    {
        using var cts = new CancellationTokenSource();
        using var handler = new Handler((_, token) =>
        {
            cts.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Ok());
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new TourenHttpClient(http).ListeGefiltert(0, 5, new(null, null, null, null), cts.Token));
        Assert.Equal(1, handler.Sends);
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(new TourListeAntwort([], 0, 0, 5)), Encoding.UTF8, "application/json") };

    private static Dictionary<string, string> ParseQuery(string query) => query.TrimStart('?').Split('&')
        .Select(part => part.Split('=', 2)).ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1]));

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Sends { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Sends++; return send(request, cancellationToken); }
    }

    private sealed class TrackingContent(string json) : HttpContent
    {
        public bool Read { get; private set; }
        public bool Disposed { get; private set; }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        { Read = true; await stream.WriteAsync(Encoding.UTF8.GetBytes(json)); }
        protected override bool TryComputeLength(out long length) { length = Encoding.UTF8.GetByteCount(json); return true; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
