using System.Text.Json;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed class SqliteTourenAnzeigeTests : IDisposable
{
    private static readonly (TourenSortierschluessel Key, bool Descending, int[] Expected)[] SortCases =
    [
        (TourenSortierschluessel.Aktivitaet, false, [2, 1, 3]), (TourenSortierschluessel.Aktivitaet, true, [1, 3, 2]),
        (TourenSortierschluessel.Kategorien, false, [3, 2, 1]), (TourenSortierschluessel.Kategorien, true, [1, 2, 3]),
        (TourenSortierschluessel.Equipment, false, [3, 2, 1]), (TourenSortierschluessel.Equipment, true, [1, 2, 3]),
        (TourenSortierschluessel.DurchschnittKmh, false, [1, 2, 3]), (TourenSortierschluessel.DurchschnittKmh, true, [3, 2, 1]),
        (TourenSortierschluessel.MaxKmh, false, [1, 2, 3]), (TourenSortierschluessel.MaxKmh, true, [3, 2, 1]),
        (TourenSortierschluessel.HfDurchschnitt, false, [1, 2, 3]), (TourenSortierschluessel.HfDurchschnitt, true, [3, 2, 1]),
        (TourenSortierschluessel.HfMax, false, [1, 2, 3]), (TourenSortierschluessel.HfMax, true, [3, 2, 1]),
        (TourenSortierschluessel.Trittfrequenz, false, [1, 2, 3]), (TourenSortierschluessel.Trittfrequenz, true, [3, 2, 1]),
        (TourenSortierschluessel.Temperatur, false, [2, 1, 3]), (TourenSortierschluessel.Temperatur, true, [3, 1, 2]),
        (TourenSortierschluessel.MaxHoehe, false, [1, 2, 3]), (TourenSortierschluessel.MaxHoehe, true, [3, 2, 1]),
        (TourenSortierschluessel.Trainingsbereich, false, [2, 3, 1]), (TourenSortierschluessel.Trainingsbereich, true, [1, 2, 3]),
        (TourenSortierschluessel.Kommentar, false, [2, 3, 1]), (TourenSortierschluessel.Kommentar, true, [1, 2, 3]),
    ];

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TourAnzeige-" + Guid.NewGuid().ToString("N"));
    private readonly string _database;
    private readonly SqliteTourenSpeicher _store;
    private readonly long[] _ids = new long[3];
    private readonly DateOnly _from = new(2026, 1, 1);
    private readonly DateOnly _to = new(2026, 1, 31);

    public SqliteTourenAnzeigeTests()
    {
        Directory.CreateDirectory(_directory);
        _database = Path.Combine(_directory, "db.sqlite");
        _store = new SqliteTourenSpeicher(_database);
        using var connection = OeffneDatenbank.Ausfuehren(_database);
        using var transaction = connection.BeginTransaction();
        var cycling = AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "Rad", IstHaupt = true });
        var running = AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "Laufen" });
        var zulu = KategorienTabelle.Einfuegen(connection, transaction, new Kategorie { Name = "Zulu" });
        var alpine = KategorienTabelle.Einfuegen(connection, transaction, new Kategorie { Name = "Alpin" });
        var retired = EquipmentsTabelle.Einfuegen(connection, transaction, new Equipment { Name = "Retired", Ausgemustert = true });
        var active = EquipmentsTabelle.Einfuegen(connection, transaction, new Equipment { Name = "Bike" });

        _ids[0] = TrainingseinheitenTabelle.Einfuegen(connection, transaction,
            Tour(cycling, retired, [zulu, alpine], new DateOnly(2026, 1, 3), "Z", 0, 0, 0, 0, 0, 0, 0, -5, 0, "GA2", "B")).Id;
        _ids[1] = TrainingseinheitenTabelle.Einfuegen(connection, transaction,
            Tour(running, active, [alpine], new DateOnly(2026, 1, 2), "A", 20, 100, 3600, 50, 120, 180, 80, 0, 1000, "GA1", "A")).Id;
        _ids[2] = TrainingseinheitenTabelle.Einfuegen(connection, transaction,
            Tour(cycling, null, [], new DateOnly(2026, 1, 1), "B", 100, 200, 7200, 60, 200, 210, 90, 10, 2000, "GA1", "A")).Id;

        TrainingseinheitenTabelle.Einfuegen(connection, transaction,
            Tour(cycling, active, [alpine], new DateOnly(2025, 12, 31), "OutsideExtreme", 9999, 9999, 999999, 999, 999, 999, 999, 99, 99999, "ZZZ", "outside"));
        transaction.Commit();
    }

    [Fact]
    public async Task Every_new_key_sorts_globally_before_one_row_paging_with_literal_expected_ids()
    {
        foreach (var sort in SortCases)
        {
            var actualIds = new List<long>();
            for (var start = 0; start < 3; start++)
            {
                var page = await _store.ListeGefiltert(start, 1, new(_from, _to, null, null)
                { Sortierung = sort.Key, Absteigend = sort.Descending });
                Assert.Equal(3, page.Gesamt);
                Assert.Equal(start, page.Start);
                var item = Assert.Single(page.Eintraege);
                Assert.NotNull(item.Anzeige);
                actualIds.Add(item.Id);
            }
            Assert.Equal(sort.Expected.Select(index => _ids[index - 1]), actualIds);
        }
    }

    [Fact]
    public async Task Projection_is_filtered_before_global_best_markers_and_preserves_raw_tour_contract()
    {
        using var connection = OeffneDatenbank.Ausfuehren(_database);
        var rawBefore = JsonSerializer.Serialize(TrainingseinheitenTabelle.Laden(connection));
        var originals = TrainingseinheitenTabelle.Laden(connection).ToDictionary(row => row.Id, TourenMapper.ToAntwort);

        var firstPage = await _store.ListeGefiltert(0, 1, new(_from, _to, null, null)
        { Sortierung = TourenSortierschluessel.Name });
        var first = Assert.Single(firstPage.Eintraege);
        Assert.Equal(_ids[1], first.Id);
        Assert.NotNull(first.Anzeige);
        Assert.False(first.Anzeige.IstBestDistanz);

        var secondPage = await _store.ListeGefiltert(1, 1, new(_from, _to, null, null)
        { Sortierung = TourenSortierschluessel.Name });
        var best = Assert.Single(secondPage.Eintraege);
        Assert.Equal(_ids[2], best.Id);
        Assert.Equal((true, true, true, true, true),
            (best.Anzeige!.IstBestDistanz, best.Anzeige.IstBestHoehenmeter, best.Anzeige.IstBestFahrzeit,
                best.Anzeige.IstBestDurchschnittKmh, best.Anzeige.IstBestMaxKmh));

        var zero = await _store.ListeGefiltert(0, 1, new(_from, _to, null, null)
        { Sortierung = TourenSortierschluessel.Name, Absteigend = true });
        var zeroDisplay = Assert.Single(zero.Eintraege).Anzeige!;
        Assert.Null(zeroDisplay.DistanzKm);
        Assert.Equal(0, zeroDisplay.Hoehenmeter);
        Assert.Equal(-5, zeroDisplay.Temperatur);
        Assert.Equal("Alpin, Zulu", zeroDisplay.Kategorien);
        Assert.Equal("Retired (ausgemustert)", zeroDisplay.Equipment);

        foreach (var page in new[] { firstPage, secondPage, zero })
        foreach (var response in page.Eintraege)
        {
            var original = originals[response.Id];
            Assert.Equal(original.Id, response.Id);
            Assert.Equal(original.Version, response.Version);
            Assert.Equal(JsonSerializer.Serialize(original.Werte), JsonSerializer.Serialize(response.Werte));
        }
        Assert.Equal(rawBefore, JsonSerializer.Serialize(TrainingseinheitenTabelle.Laden(connection)));
    }

    private static Trainingseinheit Tour(long activity, long? equipment, long[] categories, DateOnly date, string name,
        double distance, int hm, long seconds, double maxSpeed, int averagePulse, int maxPulse, int cadence,
        int temperature, int maxHeight, string area, string comment) => new()
    {
        AktivitaetId = activity,
        EquipmentId = equipment,
        KategorieIds = [.. categories],
        Datum = date,
        Name = name,
        DistanzKm = distance,
        Hoehenmeter = hm,
        Fahrzeit = TimeSpan.FromSeconds(seconds),
        MaxGeschwindigkeit = maxSpeed,
        HfDurchschnitt = averagePulse,
        HfMax = maxPulse,
        Trittfrequenz = cadence,
        Temperatur = temperature,
        MaxHoehe = maxHeight,
        Trainingsbereich = area,
        Kommentar = comment,
    };

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
