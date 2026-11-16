using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Application.Planung;

namespace AndyTours.Tests.Import;

public sealed class PlanungsVertragMapperTests
{
    [Fact]
    public void Duplicate_waypoints_unresolved_segment_and_turns_roundtrip_defensively()
    {
        var a1 = new PlanungsPosition(48, 11); var b = new PlanungsPosition(49, 12); var a2 = new PlanungsPosition(48, 11);
        var directions = Enum.GetValues<PlanungsAbbiegerichtung>();
        var turns = directions.Select((d, i) => new PlanungsAbbiegepunkt(new PlanungsPosition(48 + i * .01, 11), d, d == PlanungsAbbiegerichtung.Kreisverkehr ? 2 : null)).ToArray();
        var stand = new PlanungsStand(CourseFahrprofil.Mountainbike, new[] { a1, b, a2 }, new PlanungsTeilstueck?[]
        {
            new(new[] { new PlanungsRoutingpunkt(a1, 14.5), new PlanungsRoutingpunkt(a2, null) }, turns, false), null
        });
        var core = PlanungsVertragMapper.ToCore(stand, default);
        Assert.Equal(new[] { new Geoposition(48, 11), new Geoposition(49, 12), new Geoposition(48, 11) }, core.Wegpunkte);
        Assert.Null(core.Teilstuecke[1]);
        var dto = PlanungsVertragMapper.ToDto(core, default);
        Assert.Equal(stand.Wegpunkte, dto.Wegpunkte);
        Assert.Null(dto.Teilstuecke[1]);
        Assert.Equal(directions, dto.Teilstuecke[0]!.Abbiegepunkte.Select(t => t.Richtung));
        Assert.Equal(14.5, dto.Teilstuecke[0]!.Punkte[0].Hoehe);
        Assert.NotSame(stand.Wegpunkte, dto.Wegpunkte);
    }

    [Fact]
    public async Task Invalid_values_are_safe_and_pre_cancelled()
    {
        var ok = new PlanungsStand(CourseFahrprofil.Trekking, new[] { new PlanungsPosition(1, 2), new PlanungsPosition(3, 4) }, new PlanungsTeilstueck?[] { null });
        foreach (var bad in new[] {
            ok with { Wegpunkte = new[] { new PlanungsPosition(double.NaN, 0), new PlanungsPosition(1, 2) } },
            ok with { Profil = (CourseFahrprofil)999 },
            ok with { Teilstuecke = Array.Empty<PlanungsTeilstueck?>() },
            ok with { Teilstuecke = new PlanungsTeilstueck?[] { new(new[] { new PlanungsRoutingpunkt(new(1,2), double.NaN), new PlanungsRoutingpunkt(new(2,3), null) }, Array.Empty<PlanungsAbbiegepunkt>(), false) } }
        }) { var ex = Assert.Throws<StammdatenException>(() => PlanungsVertragMapper.ToCore(bad, default)); Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art); }
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Run(() => PlanungsVertragMapper.ToCore(ok, cts.Token)));
    }
}
