using AndyTours.Application.Stammdaten;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;
using Xunit;
namespace AndyTours.Tests.Daten;

public sealed class SqliteTourdetailQuelldatenTests
{
    [Fact]
    public async Task SortiertEtappenUndWaehltHoehenprofilVorGpsOhnePfadeInAntwort()
    {
        using var f = new Fixture();
        var late = f.Add(new DateOnly(2025, 2, 1), new TimeOnly(8, 0), "late", "gps.fit", "profile.fit");
        var nullTime = f.Add(new DateOnly(2025, 1, 1), null, "null", null, null);
        var tie2 = f.Add(new DateOnly(2025, 1, 1), new TimeOnly(9, 0), "tie2", null, null);
        var tie1 = f.Add(new DateOnly(2025, 1, 1), new TimeOnly(9, 0), "tie1", null, null);
        var result = await f.Reader.Laden([late, tie2, nullTime, tie1], CancellationToken.None);
        Assert.Equal([nullTime, tie2, tie1, late], result.Etappen.Select(x => x.Tour.Id));
        Assert.Equal("profile.fit", result.Etappen[^1].RelativerFitPfad);
        Assert.DoesNotContain("profile.fit", System.Text.Json.JsonSerializer.Serialize(result.Etappen[^1].Tour));
    }

    [Fact]
    public async Task ValidiertEingabenNichtGefundeneIdsAbsolutenPfadUndAbbruch()
    {
        using var f = new Fixture();
        foreach (long[] ids in new[] { Array.Empty<long>(), [0], [-1], [1, 1], [999999] })
        {
            var error = await Assert.ThrowsAsync<StammdatenException>(() => f.Reader.Laden(ids, CancellationToken.None));
            Assert.Equal(ids.Contains(999999) ? StammdatenFehlerart.NichtGefunden : StammdatenFehlerart.Ungueltig, error.Art);
        }
        await Assert.ThrowsAsync<StammdatenException>(() => f.Reader.Laden(null!, CancellationToken.None));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Reader.Laden([1], cts.Token));
        Assert.Throws<ArgumentException>(() => new SqliteTourdetailQuelldaten("relative.db"));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "TourdetailQuelldaten-" + Guid.NewGuid().ToString("N"));
        private readonly string _db; private readonly long _activity;
        public SqliteTourdetailQuelldaten Reader { get; }
        public Fixture()
        {
            Directory.CreateDirectory(_root); _db = Path.Combine(_root, "db.sqlite");
            using var db = OeffneDatenbank.Ausfuehren(_db); using var tx = db.BeginTransaction();
            _activity = AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Rad", IstHaupt = true }); tx.Commit();
            Reader = new SqliteTourdetailQuelldaten(_db);
        }
        public long Add(DateOnly date, TimeOnly? time, string name, string? gps, string? profile)
        {
            using var db = OeffneDatenbank.Ausfuehren(_db); using var tx = db.BeginTransaction();
            var tour = new Trainingseinheit { AktivitaetId = _activity, Datum = date, Startzeit = time, Name = name, DistanzKm = 4, Fahrzeit = TimeSpan.FromMinutes(20) };
            if (gps is not null) tour.Anhaenge.Add(new Anhang { Typ = AnhangTyp.GpsTrack, RelativerPfad = gps });
            if (profile is not null) tour.Anhaenge.Add(new Anhang { Typ = AnhangTyp.Hoehenprofil, RelativerPfad = profile });
            var saved = TrainingseinheitenTabelle.Einfuegen(db, tx, tour); tx.Commit(); return saved.Id;
        }
        public void Dispose() { SqliteConnection.ClearAllPools(); try { Directory.Delete(_root, true); } catch (IOException) { } }
    }
}
