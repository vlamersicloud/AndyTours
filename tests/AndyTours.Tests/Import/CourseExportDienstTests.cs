using AndyTours.Application.Export;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Import.Export;
using AndyTours.Import.Garmin;
using AndyTours.Tests.Garmin;
using Dynastream.Fit;
using Xunit;

namespace AndyTours.Tests.Import;

[Collection(FitTestdatei.Testgruppe)]
public sealed class CourseExportDienstTests
{
    [Fact]
    public async Task ExportiertGueltigenFitOhneRoutingkonfiguration()
    {
        var source = new FakeSource(new(Fahrprofil.Mountainbike,
            new Routingeinstellungen(Routingdienst.Openrouteservice, Routingeinstellungen.StandardBRouterAdresse, null)));
        var service = new CourseExportDienst(source,
            () => throw new InvalidOperationException("HTTP must not be created"));

        Assert.Equal(CourseFahrprofil.Mountainbike,
            (await service.LadenEinstellungen(CancellationToken.None)).StandardProfil);

        var points = new[]
        {
            Point(47.0, 12.0, 0, 0),
            Point(47.01, 12.01, 0.5, 30000),
            Point(47.02, 12.02, 1, 60000)
        };
        var result = await service.Erstellen(
            new CourseExportAnfrage("Hin/zurück", CourseFahrprofil.Trekking, points), CancellationToken.None);

        Assert.Equal("Hin-zurück.fit", result.Dateiname);
        Assert.Equal("routing_nicht_konfiguriert", result.Warncode);
        Assert.NotEmpty(result.Inhalt);
        using var stream = new MemoryStream(result.Inhalt);
        var decoder = new Decode();
        Assert.True(PruefeFitDatei.Ausfuehren(stream, decoder));
        Assert.True(decoder.CheckIntegrity(stream));
    }

    [Fact]
    public async Task FakeBRouterTurnsAreEncodedWithoutLeakingRoutingDetails()
    {
        const string json = """
        {"type":"FeatureCollection","features":[{"properties":{"voicehints":[[1,5,0,0,0]]},"geometry":{"coordinates":[[12,47,500],[12.01,47.01,510],[12.02,47.02,520]]}}]}
        """;
        var handler = new RecordingHandler(json);
        var http = new HttpClient(handler);
        var service = new CourseExportDienst(
            new FakeSource(new(Fahrprofil.Mountainbike,
                new Routingeinstellungen(Routingdienst.BRouter, "https://fake-route.invalid/", "FAKESECRET"))),
            () => http);
        var input = new[]
        {
            Point(47, 12, 0, 0),
            Point(47.01, 12.01, 0.5, 30000),
            Point(47.02, 12.02, 1, 60000)
        };

        var result = await service.Erstellen(new("track", CourseFahrprofil.Trekking, input), CancellationToken.None);
        Assert.Null(result.Warncode);
        Assert.Equal(1, handler.RequestCount);
        Assert.True(handler.Disposed);
        var publicJson = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("fake-route.invalid", publicJson);
        Assert.DoesNotContain("FAKESECRET", publicJson);

        using var stream = new MemoryStream(result.Inhalt);
        var decoder = new Decode();
        Assert.True(PruefeFitDatei.Ausfuehren(stream, decoder));
        Assert.True(decoder.CheckIntegrity(stream));
        var turns = new List<CoursePointMesg>();
        var broadcaster = new MesgBroadcaster();
        decoder.MesgEvent += broadcaster.OnMesg;
        broadcaster.CoursePointMesgEvent += (_, e) => turns.Add((CoursePointMesg)e.mesg);
        stream.Position = 0;
        decoder.Read(stream);
        var turn = Assert.Single(turns);
        Assert.Equal(CoursePoint.Right, turn.GetType());
    }

    [Fact]
    public async Task RoutingfailureDiscardsTurnsAndCancellationDoesNotRetry()
    {
        var handler = new FailingHandler();
        var service = new CourseExportDienst(
            new FakeSource(new(Fahrprofil.Mountainbike,
                new Routingeinstellungen(Routingdienst.BRouter, "https://fake-route.invalid/", "SECRET"))),
            () => new HttpClient(handler));
        var input = new[]
        {
            Point(47, 12, 0, 0), Point(47.01, 12.01, 0.5, 30000),
            Point(47.02, 12.02, 1, 60000)
        };
        var result = await service.Erstellen(new("track", CourseFahrprofil.Trekking, input), CancellationToken.None);
        Assert.Equal("routing_nicht_erreichbar", result.Warncode);
        Assert.Equal(1, handler.RequestCount);
        Assert.True(handler.Disposed);
        var turns = ReadCoursePoints(result.Inhalt);
        Assert.Empty(turns);
        Assert.False(handler.ErrorBodyRead);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.Erstellen(new("track", CourseFahrprofil.Trekking, input), cts.Token));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task UngueltigeAnfragenWerdenVorServerdatenUndNetzwerkAbgewiesen()
    {
        var source = new FakeSource(new(Fahrprofil.Mountainbike,
            new Routingeinstellungen(Routingdienst.BRouter, "https://fake.invalid/", "SECRET")));
        var factoryCalls = 0;
        var service = new CourseExportDienst(source, () =>
        {
            factoryCalls++;
            throw new InvalidOperationException("must not create HTTP client");
        });
        var valid = Point(47, 12, 0, 0);
        var cases = new[]
        {
            new CourseExportAnfrage("bad", (CourseFahrprofil)999, [valid, Point(47.1, 12.1, 1, 1)]),
            new CourseExportAnfrage("bad", CourseFahrprofil.Trekking, null!),
            new CourseExportAnfrage("bad", CourseFahrprofil.Trekking,
                [valid, Point(double.NaN, 12, 1, 1)]),
            new CourseExportAnfrage("bad", CourseFahrprofil.Trekking,
                [valid with { Hoehe = double.NaN }, Point(47.1, 12.1, 1, 1)]),
            new CourseExportAnfrage("bad", CourseFahrprofil.Trekking,
                [valid, Point(47.1, 12.1, 1, -1)]),
            new CourseExportAnfrage("bad\uD800", CourseFahrprofil.Trekking,
                [valid, Point(47.1, 12.1, 1, 1)])
        };

        foreach (var request in cases)
            await Assert.ThrowsAsync<AndyTours.Application.Stammdaten.StammdatenException>(
                () => service.Erstellen(request, CancellationToken.None));
        Assert.Equal(0, source.Reads);
        Assert.Equal(0, factoryCalls);
    }

    private static List<CoursePointMesg> ReadCoursePoints(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var decoder = new Decode();
        Assert.True(PruefeFitDatei.Ausfuehren(stream, decoder));
        Assert.True(decoder.CheckIntegrity(stream));
        var points = new List<CoursePointMesg>();
        var broadcaster = new MesgBroadcaster();
        decoder.MesgEvent += broadcaster.OnMesg;
        broadcaster.CoursePointMesgEvent += (_, e) => points.Add((CoursePointMesg)e.mesg);
        stream.Position = 0;
        decoder.Read(stream);
        return points;
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public bool Disposed { get; private set; }
        public bool ErrorBodyRead { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
            { Content = new UnreadableContent(() => ErrorBodyRead = true) });
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class UnreadableContent(Action markRead) : System.Net.Http.HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            markRead();
            throw new InvalidOperationException("error body must not be read");
        }
        protected override bool TryComputeLength(out long length) { length = -1; return false; }
        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            markRead();
            throw new InvalidOperationException("error body must not be read");
        }
    }

    private sealed class RecordingHandler(string json) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public bool Disposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private static TourdetailMesspunkt Point(double lat, double lon, double distance, long elapsed) =>
        new(DateTimeOffset.UtcNow, distance, elapsed, null, null, null, null, null, new(lat, lon));

    private sealed class FakeSource(CourseExportServerkontext context) : ICourseExportServerdaten
    {
        public int Reads { get; private set; }
        public Task<CourseExportServerkontext> Laden(CancellationToken cancellationToken)
        {
            Reads++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(context);
        }
    }
}