using System.Text.Json;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed class SaisonfortschrittAuswertungTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AndyTours-Saison-" + Guid.NewGuid().ToString("N"));
    private readonly string _database;
    private readonly long _rad;
    private readonly long _run;
    private readonly long _noGoal;
    private readonly long _categoryA;
    private readonly long _categoryB;
    private readonly long _fahrerId;

    public SaisonfortschrittAuswertungTests()
    {
        Directory.CreateDirectory(_directory);
        _database = Path.Combine(_directory, "saison.db");
        using var connection = OeffneDatenbank.Ausfuehren(_database);
        using var transaction = connection.BeginTransaction();
        _rad = AktivitaetenTabelle.Einfuegen(connection, transaction,
            new Aktivitaet { Name = "Rad", ZielKm = 3660, ZielHm = 36600, ZielAnzahl = 366 });
        _run = AktivitaetenTabelle.Einfuegen(connection, transaction,
            new Aktivitaet { Name = "Run", ZielKm = 366, ZielHm = 3660, ZielAnzahl = 36 });
        _noGoal = AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "NoGoal" });
        _categoryA = KategorienTabelle.Einfuegen(connection, transaction, new Kategorie { Name = "A" });
        _categoryB = KategorienTabelle.Einfuegen(connection, transaction, new Kategorie { Name = "B" });
        _fahrerId = FahrerTabelle.Standardfahrer(connection, transaction);
        InsertTour(connection, transaction, _fahrerId, _rad, new DateOnly(2023, 12, 31), 40, 400, 3600, [_categoryA]);
        InsertTour(connection, transaction, _fahrerId, _rad, new DateOnly(2024, 1, 1), 10, 100, 1800, [_categoryA]);
        InsertTour(connection, transaction, _fahrerId, _rad, new DateOnly(2024, 2, 29), 20, 200, 3600, [_categoryB]);
        InsertTour(connection, transaction, _fahrerId, _rad, new DateOnly(2024, 3, 1), 900, 900, 3600, [_categoryB]);
        InsertTour(connection, transaction, _fahrerId, _run, new DateOnly(2024, 2, 29), 5, 50, 900, [_categoryA]);
        transaction.Commit();
    }

    [Fact]
    public async Task Ytd_uses_activity_category_actuals_and_independent_goals_at_leap_day()
    {
        var clock = new FrozenClock(new DateTimeOffset(2024, 2, 29, 12, 0, 0, TimeSpan.Zero));
        var store = new SqliteAktivitaetsauswertung(_database, clock);
        var rawBefore = RawTraining();
        var rad = await Load(store, new DateOnly(2024, 1, 1), new DateOnly(2024, 2, 29), _rad, null);
        var season = Assert.IsType<SaisonfortschrittAntwort>(rad.Saisonfortschritt);
        Assert.Equal(new DateOnly(2024, 2, 29), season.Stichtag);
        Assert.Equal((30d, 3660d, 600d, -570d, 3630d),
            (season.Kilometer.Stand, season.Kilometer.Ziel, season.Kilometer.Soll, season.Kilometer.Abweichung, season.Kilometer.Rest));
        Assert.Equal(3630d * 7 / 307, season.Kilometer.ProWoche!.Value, 10);
        Assert.Equal(2, season.Anzahl.Stand);
        Assert.Equal(300, season.Hoehenmeter.Stand);
        Assert.Equal(1.5, season.FahrzeitStunden.Stand);
        Assert.Null(season.FahrzeitStunden.Ziel);
        Assert.Null(season.FahrzeitStunden.Soll);
        Assert.Equal(1, rad.ZukuenftigeAktivitaeten);

        var category = await Load(store, new DateOnly(2024, 1, 1), new DateOnly(2024, 2, 29), _rad, _categoryA);
        Assert.Equal(10, category.Saisonfortschritt!.Kilometer.Stand);
        Assert.Equal(3660, category.Saisonfortschritt.Kilometer.Ziel);

        var allActivities = await Load(store, new DateOnly(2024, 1, 1), new DateOnly(2024, 2, 29), null, null);
        Assert.Equal(35, allActivities.Saisonfortschritt!.Kilometer.Stand);
        Assert.Equal(4026, allActivities.Saisonfortschritt.Kilometer.Ziel);
        Assert.Equal(3, allActivities.Saisonfortschritt.Anzahl.Stand);
        Assert.Equal(350, allActivities.Saisonfortschritt.Hoehenmeter.Stand);
        Assert.Equal(1.75, allActivities.Saisonfortschritt.FahrzeitStunden.Stand);

        var noGoal = await Load(store, new DateOnly(2024, 1, 1), new DateOnly(2024, 2, 29), _noGoal, null);
        Assert.Equal(0, noGoal.Saisonfortschritt!.Kilometer.Stand);
        Assert.Null(noGoal.Saisonfortschritt.Kilometer.Ziel);
        Assert.Equal(rawBefore, RawTraining());
        Assert.Equal(4, clock.Reads);
    }

    [Fact]
    public async Task Only_exact_current_ytd_range_has_projection_and_boundaries_are_literal()
    {
        var clock = new FrozenClock(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var store = new SqliteAktivitaetsauswertung(_database, clock);
        var start = await Load(store, new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 1), _rad, null);
        Assert.Equal(10, start.Saisonfortschritt!.Kilometer.Stand);
        Assert.Equal(10, start.Saisonfortschritt.Kilometer.Soll);
        Assert.Equal(1, clock.Reads);

        var nonYtd = await Load(store, new DateOnly(2024, 1, 2), new DateOnly(2024, 2, 29), _rad, null);
        Assert.Null(nonYtd.Saisonfortschritt);
        var shortEnd = await Load(store, new DateOnly(2024, 1, 1), new DateOnly(2024, 2, 28), _rad, null);
        Assert.Null(shortEnd.Saisonfortschritt);
        var priorYear = await Load(store, new DateOnly(2023, 1, 1), new DateOnly(2023, 12, 31), _rad, null);
        Assert.Null(priorYear.Saisonfortschritt);

        var yearEndClock = new FrozenClock(new DateTimeOffset(2024, 12, 31, 12, 0, 0, TimeSpan.Zero));
        var yearEndStore = new SqliteAktivitaetsauswertung(_database, yearEndClock);
        var yearEnd = await Load(yearEndStore, new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), _rad, null);
        Assert.Equal(930, yearEnd.Saisonfortschritt!.Kilometer.Stand);
        Assert.Equal(3660, yearEnd.Saisonfortschritt.Kilometer.Soll);
        Assert.Equal(3660, yearEnd.Saisonfortschritt.Kilometer.Ziel);
        Assert.Equal(1, yearEndClock.Reads);
    }

    private static Task<AktivitaetsauswertungAntwort> Load(SqliteAktivitaetsauswertung store, DateOnly from, DateOnly to, long? activity, long? category) =>
        store.Laden(new AktivitaetsauswertungAnfrage(from, to, Auswertungsraster.Monat, activity, category));

    private static void InsertTour(SqliteConnection connection, SqliteTransaction transaction, long fahrerId, long activity, DateOnly date,
        double km, int hm, long seconds, long[] categories) =>
        TrainingseinheitenTabelle.Einfuegen(connection, transaction, new Trainingseinheit
        {
            FahrerId = fahrerId, AktivitaetId = activity, Datum = date, Name = "Season fixture", DistanzKm = km,
            Hoehenmeter = hm, Fahrzeit = TimeSpan.FromSeconds(seconds), KategorieIds = categories.ToList(),
            Trainingsbereich = "GA1", Kommentar = string.Empty
        });

    private string RawTraining()
    {
        using var connection = OeffneDatenbank.Ausfuehren(_database);
        return JsonSerializer.Serialize(TrainingseinheitenTabelle.Laden(connection));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private sealed class FrozenClock(DateTimeOffset now) : TimeProvider
    {
        public int Reads { get; private set; }
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() { Reads++; return now; }
    }
}
