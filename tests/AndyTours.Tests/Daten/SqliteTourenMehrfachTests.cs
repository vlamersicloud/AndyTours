using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed class SqliteTourenMehrfachTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AndyTours-Mehrfach-" + Guid.NewGuid().ToString("N"));
    private readonly string _database;
    private readonly SqliteTourenMehrfachverwaltung _bulk;
    private readonly SqliteTourenSpeicher _tours;
    private readonly long _activity;
    private readonly long _otherActivity;
    private readonly long _keepCategory;
    private readonly long _removeCategory;
    private readonly long _setCategory;
    private readonly long _equipment;
    private readonly long _otherEquipment;

    public SqliteTourenMehrfachTests()
    {
        Directory.CreateDirectory(_directory);
        _database = Path.Combine(_directory, "andytours.db");
        _bulk = new SqliteTourenMehrfachverwaltung(_database);
        _tours = new SqliteTourenSpeicher(_database);
        using var connection = OeffneDatenbank.Ausfuehren(_database);
        using var transaction = connection.BeginTransaction();
        _activity = AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "Rad", IstHaupt = true });
        _otherActivity = AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "Laufen" });
        _keepCategory = KategorienTabelle.Einfuegen(connection, transaction, new Kategorie { Name = "Bleibt" });
        _removeCategory = KategorienTabelle.Einfuegen(connection, transaction, new Kategorie { Name = "Entfernen" });
        _setCategory = KategorienTabelle.Einfuegen(connection, transaction, new Kategorie { Name = "Setzen" });
        _equipment = EquipmentsTabelle.Einfuegen(connection, transaction, new Equipment { Name = "Rad A", AktivitaetId = _activity });
        _otherEquipment = EquipmentsTabelle.Einfuegen(connection, transaction, new Equipment { Name = "Schuh B", AktivitaetId = _otherActivity });
        transaction.Commit();
    }

    private async Task<(TourAntwort First, TourAntwort Second)> Seed()
    {
        var first = await _tours.Anlegen(Werte(_activity, _equipment, [_keepCategory, _removeCategory], "Erste", 12.5));
        var second = await _tours.Anlegen(Werte(_otherActivity, _otherEquipment, [_removeCategory], "Zweite", 45.25));
        using var connection = OeffneDatenbank.Ausfuehren(_database);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Anhang (EinheitId, Typ, RelativerPfad)
            VALUES ($id, 'GpsTrack', '2026/test.fit');
            """;
        command.Parameters.AddWithValue("$id", first.Id);
        command.ExecuteNonQuery();
        return (first, second);
    }

    [Fact]
    public async Task Category_bulk_change_preserves_other_fields_and_replay_is_canonical_and_idempotent()
    {
        var (first, second) = await Seed();
        var requestId = Guid.NewGuid();
        var refs = new List<TourVersionsreferenz>
        {
            new(second.Id, second.Version.ToLowerInvariant()),
            new(first.Id, first.Version.ToLowerInvariant())
        };
        var request = new TourenKategorienAnfrage(requestId, refs, [_setCategory], [_removeCategory]);
        var response = await _bulk.Kategorien(request, default);

        Assert.Equal(requestId, response.AnfrageId);
        Assert.Equal(new[] { first.Id, second.Id }, response.Eintraege.Select(x => x.Id));
        Assert.Empty(response.GeloeschteIds);
        var firstAfter = await _tours.Lesen(first.Id);
        var secondAfter = await _tours.Lesen(second.Id);
        Assert.Equal(new[] { _keepCategory, _setCategory }, firstAfter!.Werte.KategorieIds);
        Assert.Equal(new[] { _setCategory }, secondAfter!.Werte.KategorieIds);
        Assert.Equal(first.Werte.Name, firstAfter.Werte.Name);
        Assert.Equal(first.Werte.Datum, firstAfter.Werte.Datum);
        Assert.Equal(first.Werte.DistanzKm, firstAfter.Werte.DistanzKm);
        Assert.Equal(first.Werte.FahrzeitSekunden, firstAfter.Werte.FahrzeitSekunden);
        Assert.Equal(first.Werte.AktivitaetId, firstAfter.Werte.AktivitaetId);
        Assert.Equal(first.Werte.EquipmentId, firstAfter.Werte.EquipmentId);
        Assert.Equal(1, ScalarInt64("SELECT COUNT(*) FROM Anhang WHERE EinheitId = $id;", first.Id));
        Assert.Equal(1, ReceiptCount());

        var replay = await _bulk.Kategorien(new TourenKategorienAnfrage(requestId,
            [new(first.Id, first.Version), new(second.Id, second.Version)],
            [_setCategory], [_removeCategory]), default);
        Assert.Equal(response.Eintraege.Select(x => x.Version), replay.Eintraege.Select(x => x.Version));
        Assert.Equal(1, ReceiptCount());
        Assert.Equal(2, (await _tours.Liste(0, 20, null, null)).Gesamt);

        await Assert.ThrowsAsync<TourVersionskonfliktException>(() => _bulk.Kategorien(
            new TourenKategorienAnfrage(requestId,
                [new(first.Id, first.Version), new(second.Id, second.Version)],
                [_keepCategory], [_removeCategory]), default));
        await Assert.ThrowsAsync<TourVersionskonfliktException>(() => _bulk.Aktivitaet(
            new TourenAktivitaetAnfrage(requestId,
                [new(first.Id, first.Version), new(second.Id, second.Version)], _activity), default));
        Assert.Equal(1, ReceiptCount());
        Assert.Equal(new[] { _keepCategory, _setCategory }, (await _tours.Lesen(first.Id))!.Werte.KategorieIds);
    }

    [Fact]
    public async Task Every_version_is_checked_before_any_row_changes_and_unknown_target_has_no_receipt()
    {
        var (first, second) = await Seed();
        var baseline = await _bulk.Kategorien(new TourenKategorienAnfrage(Guid.NewGuid(),
            [new(first.Id, first.Version)], [_setCategory], [_removeCategory]), default);
        Assert.Single(baseline.Eintraege);
        var firstAfterBaseline = await _tours.Lesen(first.Id);
        var stale = new string('A', 64);
        var request = new TourenKategorienAnfrage(Guid.NewGuid(),
            [new(first.Id, firstAfterBaseline!.Version), new(second.Id, stale)], [_keepCategory], [_setCategory]);
        await Assert.ThrowsAsync<TourVersionskonfliktException>(() => _bulk.Kategorien(request, default));
        Assert.Equal(new[] { _keepCategory, _setCategory }, (await _tours.Lesen(first.Id))!.Werte.KategorieIds);
        Assert.Equal(1, ReceiptCount());

        var missing = new TourenKategorienAnfrage(Guid.NewGuid(),
            [new(first.Id, firstAfterBaseline!.Version), new(long.MaxValue, firstAfterBaseline.Version)], [_removeCategory], []);
        await Assert.ThrowsAsync<TourNichtGefundenException>(() => _bulk.Kategorien(missing, default));
        Assert.Equal(new[] { _keepCategory, _setCategory }, (await _tours.Lesen(first.Id))!.Werte.KategorieIds);
        Assert.Equal(1, ReceiptCount());
    }

    private static TourWerte Werte(long activity, long equipment, long[] categories, string name, double distance) =>
        TourenMapper.ToAntwort(new Trainingseinheit
        {
            AktivitaetId = activity,
            Datum = new DateOnly(2026, 5, 12),
            Startzeit = new TimeOnly(8, 15),
            Name = name,
            DistanzKm = distance,
            Hoehenmeter = 321,
            Fahrzeit = TimeSpan.FromMinutes(63),
            Kommentar = "Erhalten"
        }).Werte with
        {
            KategorieIds = categories,
            EquipmentId = equipment
        };

    private long ReceiptCount() => ScalarInt64("SELECT COUNT(*) FROM ApiTourMehrfach;");

    private long ScalarInt64(string sql, long? id = null)
    {
        using var connection = OeffneDatenbank.Ausfuehren(_database);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (id.HasValue) command.Parameters.AddWithValue("$id", id.Value);
        return (long)command.ExecuteScalar()!;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
