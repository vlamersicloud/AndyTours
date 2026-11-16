using AndyTours.Application.Auswertung;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Import.Dateien;
using AndyTours.Tests.Garmin;
using Xunit;
namespace AndyTours.Tests.Import;

[Collection(FitTestdatei.Testgruppe)]
public sealed class TourdetailLeserTests
{
    [Fact]
    public async Task LiestUtcTimerpausenNullableMessungenVersionUndWindowsRelativpfad()
    {
        using var f = new Fixture();
        var tour = f.Add(@"2026\ride.fit"); f.Write("2026/ride.fit", new FitTestdatei.Werte(new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc), MitPause: true));
        var first = await f.Reader.Laden(new([tour]), CancellationToken.None); var stage = Assert.Single(first.Etappen);
        Assert.Equal(3, stage.Verlauf!.Count); Assert.Equal(new long[] { 0, 60000, 80000 }, stage.Verlauf.Select(p => p.FahrzeitMillisekunden));
        Assert.Equal(DateTimeOffset.Parse("2026-02-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture), stage.Verlauf[0].ZeitpunktUtc); Assert.Null(stage.Verlauf[0].Puls); Assert.Null(stage.Verlauf[1].Position);
        Assert.Equal(900d, stage.Verlauf[1].Hoehe); Assert.Equal(21.6d, stage.Verlauf[1].Geschwindigkeit); Assert.Equal(85, stage.Verlauf[1].Trittfrequenz); Assert.Equal(15, stage.Verlauf[1].Temperatur);
        Assert.Equal(first.Version, (await f.Reader.Laden(new([tour]), CancellationToken.None)).Version);
        var publicJson = System.Text.Json.JsonSerializer.Serialize(first); Assert.DoesNotContain(f.Root, publicJson); Assert.DoesNotContain("2026/ride.fit", publicJson);
        f.Write("2026/ride.fit", new FitTestdatei.Werte(new DateTime(2026, 2, 2, 12, 0, 0, DateTimeKind.Utc), MitPause: true));
        Assert.NotEqual(first.Version, (await f.Reader.Laden(new([tour]), CancellationToken.None)).Version);
        Assert.Empty(Directory.GetFiles(Path.Combine(f.Root, ".fit-snapshots"), ".fit-verlauf-*.tmp"));
    }

    [Fact]
    public async Task HaeuftFehlendeUngueltigeUndGueltigeEtappenMitEndlichenWarnungen()
    {
        using var f = new Fixture(); var valid = f.Add("2026/good.fit"); var missing = f.Add("2026/missing.fit"); var none = f.Add(null); var escape = f.Add("../outside.fit");
        f.Write("2026/good.fit", new FitTestdatei.Werte(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)));
        using var cts = new CancellationTokenSource(); cts.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Reader.Laden(new([valid]), cts.Token));
        var result = await f.Reader.Laden(new([escape, none, valid, missing]), CancellationToken.None); Assert.Equal(4, result.Etappen.Count);
        Assert.NotNull(result.Etappen.Single(x => x.Tour.Id == valid).Verlauf); Assert.Equal("fit_nicht_lesbar", result.Etappen.Single(x => x.Tour.Id == escape).Warncode); Assert.Equal("keine_aufzeichnung", result.Etappen.Single(x => x.Tour.Id == none).Warncode); Assert.Equal("fit_nicht_lesbar", result.Etappen.Single(x => x.Tour.Id == missing).Warncode);
        Assert.Empty(Directory.GetFiles(Path.Combine(f.Root, ".fit-snapshots"), ".fit-verlauf-*.tmp"));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "TourdetailLeser-" + Guid.NewGuid().ToString("N")); private readonly string _db; private readonly long _activity; public TourdetailLeser Reader { get; }
        public Fixture() { Directory.CreateDirectory(Root); _db = Path.Combine(Root, "db.sqlite"); using var db = OeffneDatenbank.Ausfuehren(_db); using var tx = db.BeginTransaction(); _activity = AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Rad", IstHaupt = true }); tx.Commit(); Reader = new TourdetailLeser(new SqliteTourdetailQuelldaten(_db), Root); }
        public long Add(string? path) { using var db = OeffneDatenbank.Ausfuehren(_db); using var tx = db.BeginTransaction(); var t = new Trainingseinheit { AktivitaetId = _activity, Datum = new DateOnly(2026, 2, 1), Name = "Test", DistanzKm = 1, Fahrzeit = TimeSpan.FromMinutes(1) }; if (path is not null) t.Anhaenge.Add(new Anhang { Typ = AnhangTyp.Hoehenprofil, RelativerPfad = path }); var saved = TrainingseinheitenTabelle.Einfuegen(db, tx, t); tx.Commit(); return saved.Id; }
        public void Write(string relative, FitTestdatei.Werte values) { var path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(path)!); FitTestdatei.Schreibe(path, values); }
        public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); try { Directory.Delete(Root, true); } catch (IOException) { } }
    }
}
