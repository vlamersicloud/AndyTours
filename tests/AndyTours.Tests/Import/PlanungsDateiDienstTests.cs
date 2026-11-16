using System.Text;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Import.Planung;

namespace AndyTours.Tests.Import;

public sealed class PlanungsDateiDienstTests
{
    [Fact]
    public async Task Memory_gpx_roundtrip_preserves_route_and_unresolved_segment()
    {
        var a = new PlanungsPosition(48, 11); var b = new PlanungsPosition(49, 12);
        var stand = new PlanungsStand(CourseFahrprofil.Mountainbike, new[] { a, b, a }, new PlanungsTeilstueck?[]
        {
            new(new[] { new PlanungsRoutingpunkt(a, 500.25), new PlanungsRoutingpunkt(new(48.5, 11.5), 510), new PlanungsRoutingpunkt(b, null) },
                new[] { new PlanungsAbbiegepunkt(new(48.5, 11.5), PlanungsAbbiegerichtung.Rechts, null) }, false), null
        });
        var exported = await PlanungsDateiDienst.Schreiben(new("Cafe\u0301 Trail", stand), default);
        Assert.Equal("Café Trail.gpx", exported.Dateiname);
        await using var input = new MemoryStream(exported.Inhalt);
        var read = await PlanungsDateiDienst.Lesen(input, default);
        Assert.True(input.CanRead);
        Assert.Equal(stand.Profil, read.Profil);
        Assert.Equal(stand.Wegpunkte, read.Wegpunkte);
        Assert.Equal(PlanungsAbbiegerichtung.Rechts, read.Teilstuecke[0]!.Abbiegepunkte[0].Richtung);
        Assert.Equal(500.3, read.Teilstuecke[0]!.Punkte[0].Hoehe);
        Assert.True(read.Teilstuecke[1]!.IstLuftlinie);
    }

    [Fact]
    public async Task Invalid_gpx_and_precancellation_are_safe()
    {
        foreach (var xml in new[] { "", "<gpx/>", "<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///etc/passwd'>]><gpx>&e;</gpx>" })
        {
            await using var input = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            var ex = await Assert.ThrowsAsync<StammdatenException>(() => PlanungsDateiDienst.Lesen(input, default));
            Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
            Assert.True(input.CanRead);
        }
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await using var cancelled = new MemoryStream(Encoding.UTF8.GetBytes("<gpx/>"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PlanungsDateiDienst.Lesen(cancelled, cts.Token));
    }
}
