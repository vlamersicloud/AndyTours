using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed class SqliteTourenMehrfachZielTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AndyTours-MehrfachZiel-" + Guid.NewGuid().ToString("N"));
    private readonly string _database;
    private readonly SqliteTourenMehrfachverwaltung _bulk;
    private readonly SqliteTourenSpeicher _tours;
    private readonly long _activityA;
    private readonly long _activityB;
    private readonly long _activityC;
    private readonly long _category;
    private readonly long _equipmentA;
    private readonly long _equipmentB;
    private readonly long _sharedEquipment;
    private readonly long _retiredEquipment;
    private readonly long _unrelatedEquipment;

    public SqliteTourenMehrfachZielTests()
    {
        Directory.CreateDirectory(_directory);
        _database = Path.Combine(_directory, "andytours.db");
        _bulk = new SqliteTourenMehrfachverwaltung(_database);
        _tours = new SqliteTourenSpeicher(_database);
        using var connection = OeffneDatenbank.Ausfuehren(_database);
        using var transaction = connection.BeginTransaction();
        _activityA = AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "Rad", IstHaupt = true });
        _activityB = AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "Laufen" });
        _activityC = AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "Schwimmen" });
        _category = KategorienTabelle.Einfuegen(connection, transaction, new Kategorie { Name = "Bleibt" });
        _equipmentA = EquipmentsTabelle.Einfuegen(connection, transaction, new Equipment { Name = "Rad A", AktivitaetId = _activityA });
        _equipmentB = EquipmentsTabelle.Einfuegen(connection, transaction, new Equipment { Name = "Schuh B", AktivitaetId = _activityB });
        _sharedEquipment = EquipmentsTabelle.Einfuegen(connection, transaction, new Equipment { Name = "Universal", AktivitaetId = null });
        _retiredEquipment = EquipmentsTabelle.Einfuegen(connection, transaction, new Equipment { Name = "Alt", AktivitaetId = _activityB, Ausgemustert = true });
        _unrelatedEquipment = EquipmentsTabelle.Einfuegen(connection, transaction, new Equipment { Name = "Fremd", AktivitaetId = _activityC });
        transaction.Commit();
    }

    private async Task<(TourAntwort A, TourAntwort B)> Seed()
    {
        var a = await _tours.Anlegen(Werte(_activityA, _equipmentA, "A"));
        var b = await _tours.Anlegen(Werte(_activityB, _equipmentB, "B"));
        return (a, b);
    }

    [Fact]
    public async Task Activity_preserves_incompatible_equipment_and_equipment_accepts_any_selected_activity_or_none()
    {
        var (a, b) = await Seed();
        var refs = new[] { new TourVersionsreferenz(a.Id, a.Version), new TourVersionsreferenz(b.Id, b.Version) };

        var anyMatch = await _bulk.Equipment(new TourenEquipmentAnfrage(Guid.NewGuid(), refs, _equipmentA), default);
        Assert.Equal(new[] { a.Id, b.Id }, anyMatch.Eintraege.Select(x => x.Id));
        Assert.All(anyMatch.Eintraege, x => Assert.Equal(_equipmentA, x.Werte.EquipmentId));
        Assert.All(anyMatch.Eintraege, x => Assert.Equal(_category, x.Werte.KategorieIds.Single()));

        var current = await CurrentRefs(a.Id, b.Id);
        var activity = await _bulk.Aktivitaet(new TourenAktivitaetAnfrage(Guid.NewGuid(), current, _activityB), default);
        Assert.All(activity.Eintraege, x => Assert.Equal(_activityB, x.Werte.AktivitaetId));
        Assert.All(activity.Eintraege, x => Assert.Equal(_equipmentA, x.Werte.EquipmentId));
        Assert.All(activity.Eintraege, x => Assert.Equal(_category, x.Werte.KategorieIds.Single()));

        current = await CurrentRefs(a.Id, b.Id);
        var noActivityEquipment = await _bulk.Equipment(new TourenEquipmentAnfrage(Guid.NewGuid(), current, _sharedEquipment), default);
        Assert.All(noActivityEquipment.Eintraege, x => Assert.Equal(_sharedEquipment, x.Werte.EquipmentId));

        current = await CurrentRefs(a.Id, b.Id);
        var clear = await _bulk.Equipment(new TourenEquipmentAnfrage(Guid.NewGuid(), current, null), default);
        Assert.All(clear.Eintraege, x => Assert.Null(x.Werte.EquipmentId));
        Assert.All(clear.Eintraege, x => Assert.Equal(_activityB, x.Werte.AktivitaetId));
        Assert.Equal(4, ReceiptCount());
    }

    [Fact]
    public async Task Missing_category_or_activity_and_retired_unrelated_or_unknown_equipment_are_rejected_without_receipt()
    {
        var (a, b) = await Seed();
        var baseline = await _bulk.Aktivitaet(new TourenAktivitaetAnfrage(Guid.NewGuid(),
            [new(a.Id, a.Version)], _activityA), default);
        Assert.Single(baseline.Eintraege);
        var refs = await CurrentRefs(a.Id, b.Id);
        var count = ReceiptCount();

        await AssertInvalid(() => _bulk.Kategorien(new TourenKategorienAnfrage(Guid.NewGuid(), refs, [long.MaxValue], []), default));
        await AssertInvalid(() => _bulk.Kategorien(new TourenKategorienAnfrage(Guid.NewGuid(), refs, [], [long.MaxValue]), default));
        await AssertInvalid(() => _bulk.Aktivitaet(new TourenAktivitaetAnfrage(Guid.NewGuid(), refs, long.MaxValue), default));
        await AssertInvalid(() => _bulk.Equipment(new TourenEquipmentAnfrage(Guid.NewGuid(), refs, _retiredEquipment), default));
        await AssertInvalid(() => _bulk.Equipment(new TourenEquipmentAnfrage(Guid.NewGuid(), refs, _unrelatedEquipment), default));
        await AssertInvalid(() => _bulk.Equipment(new TourenEquipmentAnfrage(Guid.NewGuid(), refs, long.MaxValue), default));

        Assert.Equal(count, ReceiptCount());
        Assert.Equal(_equipmentA, (await _tours.Lesen(a.Id))!.Werte.EquipmentId);
        Assert.Equal(_equipmentB, (await _tours.Lesen(b.Id))!.Werte.EquipmentId);
        Assert.Equal(_activityA, (await _tours.Lesen(a.Id))!.Werte.AktivitaetId);
        Assert.Equal(_activityB, (await _tours.Lesen(b.Id))!.Werte.AktivitaetId);
    }

    [Fact]
    public async Task Invalid_request_and_precancellation_do_not_create_missing_database()
    {
        var missingDirectory = Path.Combine(_directory, "not-created");
        var missingDatabase = Path.Combine(missingDirectory, "db.sqlite");
        var store = new SqliteTourenMehrfachverwaltung(missingDatabase);
        await Assert.ThrowsAsync<UngueltigeTourException>(() => store.Kategorien(
            new TourenKategorienAnfrage(Guid.Empty, [], [], []), default));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Aktivitaet(
            new TourenAktivitaetAnfrage(Guid.NewGuid(), [new(1, new string('A', 64))], 1), cts.Token));
        Assert.False(File.Exists(missingDatabase));
        Assert.False(Directory.Exists(missingDirectory));
    }

    private async Task<TourVersionsreferenz[]> CurrentRefs(long a, long b) =>
    [
        new(a, (await _tours.Lesen(a))!.Version),
        new(b, (await _tours.Lesen(b))!.Version)
    ];

    private static async Task AssertInvalid(Func<Task<TourenMehrfachAntwort>> operation)
    {
        var exception = await Assert.ThrowsAsync<UngueltigeTourException>(operation);
        Assert.Equal("Mehrfachaktion ist ungültig.", exception.Message);
    }

    private long ReceiptCount()
    {
        using var connection = OeffneDatenbank.Ausfuehren(_database);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ApiTourMehrfach;";
        return (long)command.ExecuteScalar()!;
    }

    private TourWerte Werte(long activity, long? equipment, string name) =>
        TourenMapper.ToAntwort(new Trainingseinheit
        {
            AktivitaetId = activity,
            Datum = new DateOnly(2026, 6, 7),
            Startzeit = new TimeOnly(10, 30),
            Name = name,
            DistanzKm = 22,
            Hoehenmeter = 444,
            Fahrzeit = TimeSpan.FromMinutes(55),
            Kommentar = "Preserve"
        }).Werte with { KategorieIds = [_category], EquipmentId = equipment };

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
