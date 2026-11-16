using System.Net;
using System.Text;
using System.Text.Json;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Import;

public sealed class OverlayHttpClientTests
{
    [Fact]
    public async Task Leser_und_Ordner_mutationen_senden_versionierte_requests_und_liefern_serverdaten()
    {
        var contentDto = new OverlayInhaltAntwort(7, "ITEM-V", new IReadOnlyList<OverlayPosition>[]
        {
            new[] { new OverlayPosition(1.25, 2.5) },
            new[] { new OverlayPosition(3, 4), new OverlayPosition(5, 6) }
        });
        var best = new OverlayBestandAntwort("AGG-V", OverlayFarbe.Gelb,
            new[] { new OverlayOrdnerAntwort(4, null, "Alpen") },
            new[] { new OverlayMetadatenAntwort(7, 4, "Trail", OverlayFarbe.Tuerkis, true, "ITEM-V") });
        var created = new OverlayAnlegenAntwort(best, 4);
        var success = new TrackingContent(Json(contentDto));
        var seen = new List<string>();
        var handler = new RecordingHandler((request, token) =>
        {
            seen.Add(request.Method + " " + request.RequestUri!.PathAndQuery);
            if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath.EndsWith("/inhalt", StringComparison.Ordinal))
                Assert.Equal("\"ITEM-V\"", Assert.Single(request.Headers.GetValues("If-Match")));
            if (request.Method == HttpMethod.Delete)
                Assert.Equal("\"AGG-V\"", Assert.Single(request.Headers.GetValues("If-Match")));
            if (request.Method == HttpMethod.Post)
            {
                using var json = JsonDocument.Parse(request.Content!.ReadAsStringAsync(token).GetAwaiter().GetResult());
                Assert.Equal("AGG-V", json.RootElement.GetProperty("version").GetString());
                Assert.Equal("Alpen", json.RootElement.GetProperty("name").GetString());
                Assert.Equal("44444444-4444-4444-4444-444444444444", json.RootElement.GetProperty("anfrageId").GetString());
            }
            var body = request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath.EndsWith("/inhalt", StringComparison.Ordinal)
                ? Json(contentDto) : (request.Method == HttpMethod.Post ? Json(created) : Json(best));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = request.Method == HttpMethod.Get ? success : body });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        var reader = new OverlayLeserHttpClient(http);
        var manager = new OverlayVerwaltungHttpClient(http);

        var loaded = await reader.Inhalt(7, "ITEM-V", default);
        Assert.Equal("ITEM-V", loaded.Version);
        Assert.Equal(2, loaded.Abschnitte.Count);
        Assert.Equal(1.25, loaded.Abschnitte[0][0].Breite);
        Assert.Equal(2.5, loaded.Abschnitte[0][0].Laenge);
        Assert.True(success.WasDisposed);
        var made = await manager.OrdnerAnlegen(new("AGG-V", Guid.Parse("44444444-4444-4444-4444-444444444444"), null, "Alpen"));
        Assert.Equal(4, made.Id);
        Assert.Equal("AGG-V", made.Bestand.Version);
        var deleted = await manager.OrdnerLoeschen(4, "AGG-V", default);
        Assert.Equal("AGG-V", deleted.Version);
        Assert.Equal(3, seen.Count);
    }

    [Fact]
    public async Task Gpx_import_streamt_Unicode_body_und_laesst_callerstream_offen()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("<gpx>Grüße</gpx>");
        var responseDto = new OverlayAnlegenAntwort(
            new OverlayBestandAntwort("NEXT", OverlayFarbe.Grau, Array.Empty<OverlayOrdnerAntwort>(),
                new[] { new OverlayMetadatenAntwort(9, 12, "Grüße", null, true, "ITEM") }), 9);
        var responseContent = new TrackingContent(Json(responseDto));
        var handler = new RecordingHandler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/api/v1/overlays/gpx/55555555-5555-5555-5555-555555555555", request.RequestUri!.AbsolutePath);
            Assert.Equal("?ordnerId=12", request.RequestUri.Query);
            Assert.Equal("\"OLD\"", Assert.Single(request.Headers.GetValues("If-Match")));
            Assert.Equal("Grüße.gpx", request.Content!.Headers.ContentDisposition!.FileNameStar);
            Assert.Equal("application/gpx+xml", request.Content.Headers.ContentType!.MediaType);
            Assert.Equal(bytes, await request.Content.ReadAsByteArrayAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = responseContent };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unit.test/") };
        using var input = new MemoryStream(bytes);
        var client = new OverlayGpxImportHttpClient(http);
        var result = await client.Importieren(
            new("old", Guid.Parse("55555555-5555-5555-5555-555555555555"), 12, "Grüße.gpx"), input, default);
        Assert.Equal(9, result.Id);
        Assert.True(input.CanRead);
        Assert.True(responseContent.WasDisposed);
        Assert.Equal(1, handler.Count);
    }

    private static StringContent Json<T>(T value) => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Count { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Count++; return send(request, cancellationToken); }
    }

    private sealed class TrackingContent(HttpContent inner) : HttpContent
    {
        public bool WasDisposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => inner.CopyToAsync(stream);
        protected override bool TryComputeLength(out long length) { length = 0; return true; }
        protected override void Dispose(bool disposing) { WasDisposed = true; inner.Dispose(); base.Dispose(disposing); }
    }
}
