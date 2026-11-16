using AndyTours.Application.Planung;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using AndyTours.Import.Garmin;
using AndyTours.Import.Planung;
using AndyTours.Tests.Garmin;
using Dynastream.Fit;
using Xunit;

namespace AndyTours.Tests.Import;

[Collection(FitTestdatei.Testgruppe)]
public sealed class PlanungsCourseExportDienstTests
{
    [Fact]
    public async Task Planned_geometry_exports_fit_with_fixed_time_and_no_network()
    {
        var a = new PlanungsPosition(47, 12); var b = new PlanungsPosition(47.02, 12.02);
        var stand = new PlanungsStand(CourseFahrprofil.Trekking, new[] { a, b, a }, new PlanungsTeilstueck?[]
        {
            new(new[] { new PlanungsRoutingpunkt(a, 500), new PlanungsRoutingpunkt(b, 520) }, new[] { new PlanungsAbbiegepunkt(b, PlanungsAbbiegerichtung.Rechts, null) }, false), null
        });
        var fixedTime = new FixedTimeProvider(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
        var service = new PlanungsCourseExportDienst(fixedTime);
        var result = await service.Erstellen(new("Café", stand), default);
        Assert.Equal("Café.fit", result.Dateiname); Assert.Null(result.Warncode);
        using var input = new MemoryStream(result.Inhalt); var decoder = new Decode();
        Assert.True(PruefeFitDatei.Ausfuehren(input, decoder)); Assert.True(decoder.CheckIntegrity(input));
        var turns = new List<CoursePointMesg>(); var files = new List<FileIdMesg>(); var laps = new List<LapMesg>();
        var broadcaster = new MesgBroadcaster(); decoder.MesgEvent += broadcaster.OnMesg;
        broadcaster.CoursePointMesgEvent += (_, e) => turns.Add((CoursePointMesg)e.mesg);
        broadcaster.FileIdMesgEvent += (_, e) => files.Add((FileIdMesg)e.mesg);
        broadcaster.LapMesgEvent += (_, e) => laps.Add((LapMesg)e.mesg);
        input.Position = 0; decoder.Read(input);
        Assert.Equal(CoursePoint.Right, Assert.Single(turns).GetType());
        Assert.Equal(new Dynastream.Fit.DateTime(fixedTime.GetUtcNow().UtcDateTime).GetTimeStamp(), Assert.Single(files).GetTimeCreated().GetTimeStamp());
        var lap = Assert.Single(laps);
        Assert.Equal(new Dynastream.Fit.DateTime(fixedTime.GetUtcNow().UtcDateTime).GetTimeStamp(), lap.GetStartTime().GetTimeStamp());
        var core = PlanungsVertragMapper.ToCore(stand, default);
        var expectedSeconds = BildeCourse.AusPlanung(core, "Café").Punkte[^1].Zeit.TotalSeconds;
        Assert.Equal(expectedSeconds, lap.GetTotalTimerTime()!.Value, 0.01);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.Erstellen(new("Café", stand), cts.Token));
        await Assert.ThrowsAsync<AndyTours.Application.Stammdaten.StammdatenException>(() => service.Erstellen(new("../x", stand), default));
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }
}
