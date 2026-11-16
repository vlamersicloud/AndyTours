using AndyTours.Application.Planung;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed class SqlitePlanungsStartQuelldatenTests : IDisposable
{
    private static readonly string[] ExpectedPaths = { "missing/height.fit", "missing/gps-second.fit", "missing/gps.fit" };
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "PlanStartDb-" + Guid.NewGuid().ToString("N"));
    private string DbPath => Path.Combine(_dir, "db.sqlite");

    public SqlitePlanungsStartQuelldatenTests()
    {
        Directory.CreateDirectory(_dir);
        using var db = OeffneDatenbank.Ausfuehren(DbPath);
        using var tx = db.BeginTransaction();
        var activity = AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Rad" });
        var equipment = EquipmentsTabelle.Einfuegen(db, tx, new Equipment { Name = "Bike", AktivitaetId = activity });
        TrainingseinheitenTabelle.Einfuegen(db, tx, Tour(activity, equipment, new DateOnly(2025, 1, 1), new TimeOnly(8, 0), "old",
            new Anhang { Typ = AnhangTyp.GpsTrack, RelativerPfad = "missing/gps.fit" }));
        TrainingseinheitenTabelle.Einfuegen(db, tx, Tour(activity, equipment, new DateOnly(2026, 1, 1), new TimeOnly(8, 0), "tie-first",
            new Anhang { Typ = AnhangTyp.Dokumentation, RelativerPfad = "docs/page.html" }, new Anhang { Typ = AnhangTyp.GpsTrack, RelativerPfad = "missing/gps-tie.fit" }, new Anhang { Typ = AnhangTyp.Hoehenprofil, RelativerPfad = "missing/height.fit" }));
        TrainingseinheitenTabelle.Einfuegen(db, tx, Tour(activity, equipment, new DateOnly(2026, 1, 1), new TimeOnly(8, 0), "tie-second",
            new Anhang { Typ = AnhangTyp.GpsTrack, RelativerPfad = "missing/gps-second.fit" }));
        TrainingseinheitenTabelle.Einfuegen(db, tx, Tour(activity, equipment, new DateOnly(2026, 2, 1), new TimeOnly(9, 0), "newer",
            new Anhang { Typ = AnhangTyp.Dokumentation, RelativerPfad = "docs/only.html" }));
        tx.Commit();
    }

    [Fact]
    public async Task Returns_all_metadata_paths_in_legacy_order_and_never_checks_files()
    {
        var paths = await new SqlitePlanungsStartQuelldaten(DbPath).Laden(default);
        Assert.Equal(ExpectedPaths, paths);
        Assert.IsAssignableFrom<IReadOnlyList<string>>(paths);
        Assert.Throws<ArgumentException>(() => new SqlitePlanungsStartQuelldaten("relative.db"));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SqlitePlanungsStartQuelldaten(DbPath).Laden(cts.Token));
        using var verify = OeffneDatenbank.Ausfuehren(DbPath);
        Assert.Equal(4, TrainingseinheitenTabelle.Zaehlen(verify));
    }

    private static Trainingseinheit Tour(long activity, long equipment, DateOnly date, TimeOnly time, string name, params Anhang[] attachments) =>
        new() { AktivitaetId = activity, EquipmentId = equipment, Datum = date, Startzeit = time, Name = name,
            DistanzKm = 1, Trainingsbereich = "GA1", Anhaenge = attachments.ToList() };

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}
