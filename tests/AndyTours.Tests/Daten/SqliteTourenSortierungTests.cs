using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed class SqliteTourenSortierungTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AndyTours-TourSort-" + Guid.NewGuid().ToString("N"));
    private readonly string _db;
    private readonly SqliteTourenSpeicher _store;
    private readonly long _activity;
    private readonly long _otherActivity;
    private readonly long _category;

    public SqliteTourenSortierungTests()
    {
        Directory.CreateDirectory(_dir);
        _db = Path.Combine(_dir, "db.sqlite");
        _store = new SqliteTourenSpeicher(_db);
        using var c = OeffneDatenbank.Ausfuehren(_db);
        using var tx = c.BeginTransaction();
        _activity = AktivitaetenTabelle.Einfuegen(c, tx, new Aktivitaet { Name = "Rad", IstHaupt = true });
        _otherActivity = AktivitaetenTabelle.Einfuegen(c, tx, new Aktivitaet { Name = "Laufen" });
        _category = KategorienTabelle.Einfuegen(c, tx, new Kategorie { Name = "Filter" });
        tx.Commit();
    }

    private sealed record Row(long Id, DateOnly Date, TimeOnly? Time, string Name, double Km, int Hm, TimeSpan Duration);

    private async Task<Row[]> Seed()
    {
        var source = new (DateOnly date, TimeOnly? time, string name, double km, int hm, int minutes)[]
        {
            (new(2026, 3, 1), new(8, 0), "Öl", 30, 400, 70),
            (new(2026, 3, 2), null, "Ähre", 10, 900, 20),
            (new(2026, 3, 2), new(7, 0), "Zug", 50, 100, 90),
            (new(2026, 3, 2), new(7, 0), "Aal", 20, 700, 40),
            (new(2026, 3, 3), new(9, 0), "Uber", 40, 300, 60),
            (new(2026, 3, 1), null, "Öde", 60, 200, 10)
        };
        var rows = new List<Row>();
        foreach (var x in source)
        {
            var model = new Trainingseinheit
            {
                AktivitaetId = _activity, Datum = x.date, Startzeit = x.time, Name = x.name,
                DistanzKm = x.km, Hoehenmeter = x.hm, Fahrzeit = TimeSpan.FromMinutes(x.minutes)
            };
            var created = await _store.Anlegen(TourenMapper.ToAntwort(model).Werte);
            rows.Add(new(created.Id, x.date, x.time, x.name, x.km, x.hm, TimeSpan.FromMinutes(x.minutes)));
        }
        return rows.ToArray();
    }

    [Fact]
    public async Task Every_sort_key_and_direction_is_stable_across_small_pages()
    {
        var rows = await Seed();
        foreach (var key in Enum.GetValues<TourenSortierschluessel>().Where(x => x != TourenSortierschluessel.Bestand))
        foreach (var descending in new[] { false, true })
        {
            IOrderedEnumerable<Row> expected = key switch
            {
                TourenSortierschluessel.Datum => descending ? rows.OrderByDescending(x => x.Date).ThenByDescending(x => x.Time).ThenBy(x => x.Id) : rows.OrderBy(x => x.Date).ThenBy(x => x.Time).ThenBy(x => x.Id),
                TourenSortierschluessel.Startzeit => descending ? rows.OrderByDescending(x => x.Time).ThenBy(x => x.Id) : rows.OrderBy(x => x.Time).ThenBy(x => x.Id),
                TourenSortierschluessel.Name => descending ? rows.OrderByDescending(x => x.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("de-DE"), false)).ThenBy(x => x.Id) : rows.OrderBy(x => x.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("de-DE"), false)).ThenBy(x => x.Id),
                TourenSortierschluessel.DistanzKm => descending ? rows.OrderByDescending(x => x.Km).ThenBy(x => x.Id) : rows.OrderBy(x => x.Km).ThenBy(x => x.Id),
                TourenSortierschluessel.Hoehenmeter => descending ? rows.OrderByDescending(x => x.Hm).ThenBy(x => x.Id) : rows.OrderBy(x => x.Hm).ThenBy(x => x.Id),
                TourenSortierschluessel.Fahrzeit => descending ? rows.OrderByDescending(x => x.Duration).ThenBy(x => x.Id) : rows.OrderBy(x => x.Duration).ThenBy(x => x.Id),
                _ => throw new InvalidOperationException()
            };
            var ids = new List<long>();
            for (var start = 0; start < rows.Length; start += 2)
            {
                var page = await _store.ListeGefiltert(start, 2, new(null, null, null, null) { Sortierung = key, Absteigend = descending });
                Assert.Equal(rows.Length, page.Gesamt);
                ids.AddRange(page.Eintraege.Select(x => x.Id));
            }
            Assert.Equal(expected.Select(x => x.Id), ids);
        }
    }

    [Fact]
    public async Task Bestand_ignores_direction_and_explicit_date_desc_ties_by_descending_start_then_id()
    {
        var rows = await Seed();
        var legacy = await _store.ListeGefiltert(0, 20, new(null, null, null, null) { Absteigend = true });
        Assert.Equal(rows.OrderBy(x => x.Date).ThenBy(x => x.Time).ThenBy(x => x.Id).Select(x => x.Id), legacy.Eintraege.Select(x => x.Id));
        var desc = await _store.ListeGefiltert(0, 20, new(null, null, null, null) { Sortierung = TourenSortierschluessel.Datum, Absteigend = true });
        Assert.Equal(new[] { rows[4].Id, rows[2].Id, rows[3].Id, rows[1].Id, rows[0].Id, rows[5].Id }, desc.Eintraege.Select(x => x.Id));
    }

    [Fact]
    public async Task Filters_exclude_extreme_rows_before_descending_sort_count_and_paging()
    {
        var date = new DateOnly(2026, 4, 10);
        var first = await Create(_activity, [_category], date, "match20", 20);
        var second = await Create(_activity, [_category], date, "match10", 10);
        await Create(_otherActivity, [_category], date, "other-activity", 1000);
        await Create(_activity, [], date, "missing-category", 2000);
        await Create(_activity, [_category], date.AddDays(-1), "out-of-date", 3000);
        var filter = new TourenListenfilter(date, date, _activity, _category)
        { Sortierung = TourenSortierschluessel.DistanzKm, Absteigend = true };
        var page0 = await _store.ListeGefiltert(0, 1, filter);
        var page1 = await _store.ListeGefiltert(1, 1, filter);
        var page2 = await _store.ListeGefiltert(2, 1, filter);
        Assert.Equal(2, page0.Gesamt);
        Assert.Equal(2, page1.Gesamt);
        Assert.Equal(2, page2.Gesamt);
        Assert.Equal(first, Assert.Single(page0.Eintraege).Id);
        Assert.Equal(second, Assert.Single(page1.Eintraege).Id);
        Assert.Empty(page2.Eintraege);
    }

    private async Task<long> Create(long activity, long[] categories, DateOnly date, string name, double distance)
    {
        var values = TourenMapper.ToAntwort(new Trainingseinheit
        { AktivitaetId = activity, Datum = date, Name = name, DistanzKm = distance }).Werte with { KategorieIds = categories };
        return (await _store.Anlegen(values)).Id;
    }

    [Fact]
    public async Task Invalid_sort_is_rejected_before_database_open_and_precancelled_call_is_rejected()
    {
        var missing = Path.Combine(_dir, "must-not-be-created.db");
        var other = new SqliteTourenSpeicher(missing);
        await Assert.ThrowsAsync<UngueltigeTourException>(() => other.ListeGefiltert(0, 5, new(null, null, null, null) { Sortierung = (TourenSortierschluessel)99 }));
        Assert.False(File.Exists(missing));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.ListeGefiltert(0, 5, new(null, null, null, null), cts.Token));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
