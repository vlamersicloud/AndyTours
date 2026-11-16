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

    private static TourdetailMesspunkt Point(double lat, double lon, double distance, long elapsed) =>
        new(DateTimeOffset.UtcNow, distance, elapsed, null, null, null, null, null, new(lat, lon));

    private sealed class FakeSource(CourseExportServerkontext context) : ICourseExportServerdaten
    {
        public Task<CourseExportServerkontext> Laden(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(context);
        }
    }
}