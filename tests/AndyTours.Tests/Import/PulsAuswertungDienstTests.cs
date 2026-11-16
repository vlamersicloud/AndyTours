using AndyTours.Application.Auswertung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Import.Auswertung;
using AndyTours.Tests.Garmin;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Import;

[Collection(FitTestdatei.Testgruppe)]
public sealed class PulsAuswertungDienstTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PulseAnalysis-" + Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(_root, "db.sqlite");

    public PulsAuswertungDienstTests()
    {
        Directory.CreateDirectory(_root);
        using var db = OeffneDatenbank.Ausfuehren(Database);
    }

    [Fact]
    public async Task UsesMeasuredIntervalsExactProfileAndCalendarKeyWithoutNetwork()
    {
        long activityId;
        long categoryId;
        long tourId;
        using (var db = OeffneDatenbank.Ausfuehren(Database))
        using (var tx = db.BeginTransaction())
        {
            activityId = AktivitaetenTabelle.Einfuegen(db, tx,
                new Aktivitaet { Name = "Rad", IstHaupt = true });
            categoryId = KategorienTabelle.Einfuegen(db, tx,
                new Kategorie { Name = "Standard", IstStandard = true });
            var tour = new Trainingseinheit
            {
                AktivitaetId = activityId,
                Datum = new DateOnly(2026, 1, 15),
                Name = "Pulsfahrt",
                Fahrzeit = TimeSpan.FromMinutes(5),
                DistanzKm = 1
            };
            tour.KategorieIds.Add(categoryId);
            tour.Anhaenge.Add(new Anhang { Typ = AnhangTyp.Hoehenprofil, RelativerPfad = "pulse.fit" });
            tourId = TrainingseinheitenTabelle.Einfuegen(db, tx, tour).Id;
            GarminPulszonenEinstellung.Speichern(db, tx,
                new GarminPulszonenStand(new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero),
                    [new GarminPulsprofil("CYCLING", [90, 110, 130, 150, 170])]));
            tx.Commit();
        }

        FitTestdatei.Schreibe(Path.Combine(_root, "pulse.fit"),
            new FitTestdatei.Werte(new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc), MitPause: true));
        var service = new PulsAuswertungDienst(new SqlitePulsQuelldaten(Database), _root,
            () => throw new InvalidOperationException("Analysis must remain offline"));
        var cycling = await service.Auswerten(
            new(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 31),
                Auswertungsraster.Monat, activityId, categoryId, "CYCLING"),
            CancellationToken.None);

        Assert.Equal(1, cycling.Aktivitaeten);
        Assert.Equal(0, cycling.OhneDaten);
        Assert.Equal(20, cycling.MesszeitSekunden);
        Assert.Equal(300, cycling.FahrzeitSekunden);
        Assert.Equal("CYCLING", cycling.VerwendetesProfil);
        Assert.Equal(new DateOnly(2026, 1, 10), Assert.Single(cycling.Perioden).Von);
        Assert.Equal(new DateOnly(2026, 1, 1), cycling.Perioden[0].Schluessel);
        Assert.Collection(cycling.Referenz.Profile.Single().Untergrenzen,
            value => Assert.Equal(90, value), value => Assert.Equal(110, value), value => Assert.Equal(130, value),
            value => Assert.Equal(150, value), value => Assert.Equal(170, value));
        var cell = Assert.Single(cycling.Zellen);
        Assert.Equal(1, cell.Bereich);
        Assert.Equal(20, cell.Sekunden);
        Assert.Equal(1, cell.Anteil);

        var bins = await service.Auswerten(
            new(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 31),
                Auswertungsraster.Monat, activityId, categoryId, null),
            CancellationToken.None);
        Assert.Equal(90, Assert.Single(bins.Zellen).Bereich);

        await Assert.ThrowsAsync<StammdatenException>(() => service.Auswerten(
            new(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 31),
                Auswertungsraster.Monat, activityId, categoryId, "RUNNING"),
            CancellationToken.None));
        Assert.True(tourId > 0);
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, ".fit-snapshots"), ".fit-verlauf-*.tmp"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }
}
