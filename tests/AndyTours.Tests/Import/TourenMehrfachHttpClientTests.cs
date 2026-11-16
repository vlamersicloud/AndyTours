using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AndyTours.Application.Touren;
using AndyTours.Client;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;

namespace AndyTours.Tests.Import;

public sealed partial class TourenMehrfachHttpClientTests
{
    private static readonly string[] ExpectedPaths = ["/api/v1/touren/mehrfach/kategorien", "/api/v1/touren/mehrfach/aktivitaet", "/api/v1/touren/mehrfach/equipment", "/api/v1/touren/mehrfach/loeschen"];

    [Fact]
    public async Task All_operations_send_canonical_requests_and_return_valid_acknowledgements()
    {
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var call = 0;
        var refs = new[] { new TourVersionsreferenz(9, new string('b', 64)), new TourVersionsreferenz(3, new string('a', 64)) };
        var handler = new InspectingHandler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            var path = request.RequestUri!.AbsolutePath;
            var json = await request.Content!.ReadAsStringAsync(token);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var id = ids[call++];
            Assert.Equal(id, root.GetProperty("anfrageId").GetGuid());
            var tours = root.GetProperty("touren").EnumerateArray().ToArray();
            Assert.Equal(new long[] { 3, 9 }, tours.Select(x => x.GetProperty("id").GetInt64()));
            Assert.Equal(new[] { new string('A', 64), new string('B', 64) },
                tours.Select(x => x.GetProperty("version").GetString()));
            if (path.EndsWith("/kategorien", StringComparison.Ordinal))
            {
                Assert.True(root.TryGetProperty("setzen", out _));
                Assert.True(root.TryGetProperty("entfernen", out _));
            }
            if (path.EndsWith("/equipment", StringComparison.Ordinal))
                Assert.Equal(JsonValueKind.Null, root.GetProperty("equipmentId").ValueKind);

            if (path.EndsWith("/loeschen", StringComparison.Ordinal))
                return JsonResponse(new TourenMehrfachAntwort(id, [], [3, 9]));
            var values = TourenMapper.ToAntwort(new Trainingseinheit
            {
                Datum = new DateOnly(2026, 1, 2), Name = "Tour", DistanzKm = 1,
                AktivitaetId = 1, FahrerId = 1
            }).Werte;
            return JsonResponse(new TourenMehrfachAntwort(id,
                [new TourAntwort(3, new string('C', 64), values), new TourAntwort(9, new string('D', 64), values)], []));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new TourenMehrfachHttpClient(http);

        var categories = await client.Kategorien(new TourenKategorienAnfrage(ids[0], refs, [2], [4]), default);
        Assert.Equal(new long[] { 3, 9 }, categories.Eintraege.Select(x => x.Id));
        Assert.Equal("https://example.test/", http.BaseAddress!.ToString());
        Assert.Equal(1, handler.DisposedRequestContent);

        var activity = await client.Aktivitaet(new TourenAktivitaetAnfrage(ids[1], refs, 1), default);
        Assert.Equal(2, activity.Eintraege.Count);
        var equipment = await client.Equipment(new TourenEquipmentAnfrage(ids[2], refs, null), default);
        Assert.Equal(2, equipment.Eintraege.Count);
        var deleted = await client.Loeschen(new TourenLoeschenAnfrage(ids[3], refs), default);
        Assert.Equal(new long[] { 3, 9 }, deleted.GeloeschteIds);
        Assert.Equal(ExpectedPaths, handler.Paths);
        Assert.Equal(4, handler.SendCount);
        Assert.Equal(4, handler.DisposedRequestContent);
        Assert.Equal(4, handler.DisposedResponseContent);
    }

    [Fact]
    public async Task Invalid_or_precancelled_requests_are_not_sent()
    {
        var handler = new InspectingHandler((_, _) => throw new Xunit.Sdk.XunitException("Unexpected send."));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new TourenMehrfachHttpClient(http);
        await Assert.ThrowsAsync<UngueltigeTourException>(() =>
            client.Kategorien(new TourenKategorienAnfrage(Guid.Empty, [], [], []), default));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Loeschen(new TourenLoeschenAnfrage(Guid.NewGuid(), []), cts.Token));
        Assert.Equal(0, handler.SendCount);
    }

    private static HttpResponseMessage JsonResponse(TourenMehrfachAntwort answer) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(answer) };

    private sealed class InspectingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public int SendCount { get; private set; }
        public int DisposedRequestContent { get; private set; }
        public int DisposedResponseContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            Paths.Add(request.RequestUri!.AbsolutePath);
            var content = new DisposeTrackingContent(request.Content!, () => DisposedRequestContent++);
            request.Content = content;
            var response = await respond(request, cancellationToken);
            if (response.Content is { } responseContent)
                response.Content = new DisposeTrackingContent(responseContent, () => DisposedResponseContent++);
            return response;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }

    private sealed class DisposeTrackingContent : HttpContent
    {
        private readonly HttpContent _inner;
        private readonly Action _disposed;
        public DisposeTrackingContent(HttpContent inner, Action disposed)
        {
            _inner = inner;
            _disposed = disposed;
            foreach (var header in inner.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            await _inner.CopyToAsync(stream);
        protected override bool TryComputeLength(out long length) { length = -1; return false; }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _inner.Dispose(); _disposed(); }
            base.Dispose(disposing);
        }
    }
}
