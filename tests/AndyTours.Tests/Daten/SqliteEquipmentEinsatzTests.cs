using AndyTours.Application.Auswertung;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace AndyTours.Tests.Daten;

public sealed class SqliteEquipmentEinsatzTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "EquipmentEinsatz-" + Guid.NewGuid().ToString("N"));
    private readonly string _db;
    private readonly long _activity;

    public SqliteEquipmentEinsatzTests()
    {
        Directory.CreateDirectory(_dir);
        _db = Path.Combine(_dir, "db.sqlite");
        using var db = OeffneDatenbank.Ausfuehren(_db);
        using var tx = db.BeginTransaction();
        _activity = AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Rad" });
        var active = EquipmentsTabelle.Einfuegen(db, tx, new Equipment { Name = "Aktiv", AnfangsdistanzKm = 100 });
        var retired = EquipmentsTabelle.Einfuegen(db, tx, new Equipment { Name = "Ausgemustert", Ausgemustert = true, AnfangsdistanzKm = 250 });
        EquipmentsTabelle.Einfuegen(db, tx, new Equipment { Name = "Nur Anfang", AnfangsdistanzKm = 50 });
        var tiny = EquipmentsTabelle.Einfuegen(db, tx, new Equipment { Name = "Klein" });
        EquipmentsTabelle.Einfuegen(db, tx, new Equipment { Name = "Unbenutzt" });
        EinstellungTabelle.Schreiben(db, tx, "EquipmentEinsatz.TestSentinel", "unchanged");
        TrainingseinheitenTabelle.Einfuegen(db, tx, Tour(active, 10.25, 55));
        TrainingseinheitenTabelle.Einfuegen(db, tx, Tour(active, 0.2, 0));
        TrainingseinheitenTabelle.Einfuegen(db, tx, Tour(retired, 20, 9));
        TrainingseinheitenTabelle.Einfuegen(db, tx, Tour(tiny, 0.2, 0));
        TrainingseinheitenTabelle.Einfuegen(db, tx, Tour(null, 5, 3));
        tx.Commit();
        db.Close();
        using var legacy = OeffneDatenbank.Ausfuehren(_db);
        using (var pragma = legacy.CreateCommand()) { pragma.CommandText = "PRAGMA foreign_keys = OFF;"; pragma.ExecuteNonQuery(); }
        using var legacyTx = legacy.BeginTransaction();
        TrainingseinheitenTabelle.Einfuegen(legacy, legacyTx, Tour(999999, 7, 4));
        legacyTx.Commit();
    }

    [Fact]
    public async Task Maps_legacy_core_rows_and_does_not_mutate_source_data()
    {
        using var db = OeffneDatenbank.Ausfuehren(_db);
        var tours = TrainingseinheitenTabelle.Laden(db);
        var equipment = EquipmentsTabelle.Laden(db);
        var expected = ErmittleEquipmenteinsatz.Ausfuehren(tours, equipment);
        var toursBefore = JsonSerializer.Serialize(TrainingseinheitenTabelle.Laden(db));
        var equipmentBefore = equipment.Select(e => (e.Id, e.Name, e.AktivitaetId, e.Geraetezuordnung, e.StandardKategorie, e.IstStandard, e.Ausgemustert, e.Anschaffungsdatum, e.AnfangsdistanzKm)).ToArray();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT Schluessel, Wert FROM Einstellung ORDER BY Schluessel;";
        var settingsBefore = ReadRows(command);

        var result = await new SqliteEquipmentEinsatz(_db).Laden();

        Assert.Equal(expected.Count, result.Zeilen.Count);
        Assert.Equal(expected.Select(ToTuple), result.Zeilen.Select(ToTuple));
        Assert.IsAssignableFrom<IReadOnlyList<Contracts.V1.EquipmentEinsatzzeileAntwort>>(result.Zeilen);
        Assert.True(result.Zeilen[^1].IstSumme);
        Assert.Contains(result.Zeilen, row => row.Name == "ohne Equipment");
        Assert.Contains(result.Zeilen, row => row.Name == "Ausgemustert" + EquipmentAnzeigename.ZusatzAusgemustert);
        var initialOnlyRow = result.Zeilen.Single(row => row.Name == "Nur Anfang");
        Assert.Equal(50, initialOnlyRow.VorherKm);
        Assert.Equal(50, initialOnlyRow.GesamtKm);
        Assert.Null(initialOnlyRow.GesamtHm);
        Assert.Null(result.Zeilen.Single(row => row.Name == "Klein").GesamtKm);
        Assert.Null(result.Zeilen.Single(row => row.Name == "Klein").GesamtHm);
        var activeRow = result.Zeilen.Single(row => row.Name == "Aktiv");
        Assert.Equal(100, activeRow.VorherKm);
        Assert.Equal(110.45, activeRow.GesamtKm!.Value, 8);
        Assert.Equal(55, activeRow.GesamtHm);
        var summary = result.Zeilen[^1];
        Assert.Equal(400, summary.VorherKm!.Value, 8);
        Assert.Equal(442.65, summary.GesamtKm!.Value, 8);
        Assert.Equal(71, summary.GesamtHm);

        command.CommandText = "SELECT Schluessel, Wert FROM Einstellung ORDER BY Schluessel;";
        Assert.Equal(settingsBefore, ReadRows(command));
        Assert.Equal(toursBefore, JsonSerializer.Serialize(TrainingseinheitenTabelle.Laden(db)));
        Assert.Equal(equipmentBefore, EquipmentsTabelle.Laden(db).Select(e => (e.Id, e.Name, e.AktivitaetId, e.Geraetezuordnung, e.StandardKategorie, e.IstStandard, e.Ausgemustert, e.Anschaffungsdatum, e.AnfangsdistanzKm)).ToArray());
    }

    [Fact]
    public async Task Empty_database_returns_empty_rows_without_summary()
    {
        using var db = OeffneDatenbank.Ausfuehren(_db);
        using var tx = db.BeginTransaction();
        using (var cmd = db.CreateCommand()) { cmd.Transaction = tx; cmd.CommandText = "DELETE FROM Trainingseinheit; DELETE FROM Equipment;"; cmd.ExecuteNonQuery(); }
        tx.Commit();
        Assert.Empty((await new SqliteEquipmentEinsatz(_db).Laden()).Zeilen);
    }

    [Fact]
    public async Task Pre_cancelled_read_does_not_create_database()
    {
        var missing = Path.Combine(_dir, "not-created.sqlite");
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SqliteEquipmentEinsatz(missing).Laden(cts.Token));
        Assert.False(File.Exists(missing));
    }

    private Trainingseinheit Tour(long? equipment, double distance, int hm) => new()
    {
        AktivitaetId = _activity,
        Datum = new DateOnly(2026, 1, 2),
        Name = "Tour",
        Fahrzeit = TimeSpan.FromMinutes(20),
        DistanzKm = distance,
        Hoehenmeter = hm,
        Trainingsbereich = "GA1",
        EquipmentId = equipment,
    };

    private static string[] ReadRows(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read()) rows.Add($"{reader.GetString(0)}={reader.GetString(1)}");
        return rows.ToArray();
    }

    private static (string, double?, double?, int?, bool) ToTuple(Einsatzzeile row) => (row.Name, row.VorherKm, row.GesamtKm, row.GesamtHm, row.IstSumme);
    private static (string, double?, double?, int?, bool) ToTuple(Contracts.V1.EquipmentEinsatzzeileAntwort row) => (row.Name, row.VorherKm, row.GesamtKm, row.GesamtHm, row.IstSumme);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}
