using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiTourdetailMapperTests
{
    [Fact]
    public void MapperErhaeltTourfelderKategorienUndKopiertKategorien()
    {
        var categories = new long[] { 7, 11 };
        var values = new TourWerte(3, new DateOnly(2025, 4, 5), new TimeOnly(9, 30), "Alpenrunde",
            3661, 123.4, 1500, 1200, 400, 65.2, 142, 180, 55, 82, 18, 205.5, 72.3,
            60, "Tempo", "Notiz", 9, 13, categories);
        var answer = new TourdetailAntwort("v1",
            [new TourdetailEtappeAntwort(new TourAntwort(42, "tour-v", values), "fit-v", [], null)]);
        var stage = Assert.Single(ApiTourdetailMapper.AlsEtappen(answer));
        Assert.Equal(42, stage.Einheit.Id);
        Assert.Equal("Alpenrunde", stage.Einheit.Name);
        Assert.Equal(3661, stage.Einheit.Fahrzeit.TotalSeconds);
        Assert.Equal(123.4, stage.Einheit.DistanzKm);
        Assert.Equal(1500, stage.Einheit.Hoehenmeter);
        Assert.Equal(new long[] { 7, 11 }, stage.Einheit.KategorieIds);
        Assert.NotSame(categories, stage.Einheit.KategorieIds);
    }

    [Fact]
    public void MapperErhaeltEtappenreihenfolgeVerlaufLueckenUndMesswerte()
    {
        var utc = new DateTimeOffset(2025, 5, 6, 7, 8, 9, TimeSpan.Zero).AddMilliseconds(123);
        TourAntwort Tour(long id) => new(id, "v", new TourWerte(1, new DateOnly(2025, 5, 6), null,
            $"Tour {id}", 60, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", "", 1, null, []));
        var point = new TourdetailMesspunkt(utc, 2.5, 1234, null, 120, 30.5, null, 17,
            new TourdetailPosition(48.1, 11.6));
        var answer = new TourdetailAntwort("v",
        [
            new TourdetailEtappeAntwort(Tour(2), null, null, "missing"),
            new TourdetailEtappeAntwort(Tour(1), "fit", [point], null),
            new TourdetailEtappeAntwort(Tour(3), "fit-empty", [], null)
        ]);

        var stages = ApiTourdetailMapper.AlsEtappen(answer);
        Assert.Equal(new long[] { 2, 1, 3 }, stages.Select(s => s.Einheit.Id));
        Assert.Null(stages[0].Verlauf);
        var mapped = Assert.Single(stages[1].Verlauf!);
        Assert.Equal(utc.UtcDateTime, mapped.ZeitpunktUtc);
        Assert.Equal(TimeSpan.FromMilliseconds(1234), mapped.Fahrzeit);
        Assert.Null(mapped.Hoehe);
        Assert.Equal(120, mapped.Puls);
        Assert.Equal(30.5, mapped.Geschwindigkeit);
        Assert.Null(mapped.Trittfrequenz);
        Assert.Equal(17, mapped.Temperatur);
        Assert.Equal(48.1, mapped.Position!.Breite);
        Assert.Equal(11.6, mapped.Position.Laenge);
        Assert.NotNull(stages[2].Verlauf);
        Assert.Empty(stages[2].Verlauf!);
    }
}