using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

/// <summary>Isolierte Regressionen für Wartungs- und Alarmverwaltung.</summary>
public sealed class SqliteWartungsverwaltungTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AndyTours-WartungsApi-" + Guid.NewGuid().ToString("N"));
    private readonly string _database;
    private readonly SqliteWartungsverwaltung _service;
    private readonly long _activity1, _activity2, _equipment1, _equipment2, _retired;

    public SqliteWartungsverwaltungTests()
    {
        Directory.CreateDirectory(_directory);
        _database = Path.Combine(_directory, "andytours.db");
        _service = new SqliteWartungsverwaltung(_database);
        using var connection = OeffneDatenbank.Ausfuehren(_database);
        using var transaction = connection.BeginTransaction();
        _activity1 = AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "Rad", IstHaupt = true });
        _activity2 = AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "Laufen" });
        _equipment1 = EquipmentsTabelle.Einfuegen(connection, transaction, new Equipment { Name = "Rad A", AktivitaetId = _activity1, Anschaffungsdatum = new DateOnly(2020, 1, 1) });
        _equipment2 = EquipmentsTabelle.Einfuegen(connection, transaction, new Equipment { Name = "Rad B", AktivitaetId = _activity1 });
        _retired = EquipmentsTabelle.Einfuegen(connection, transaction, new Equipment { Name = "Alt", AktivitaetId = _activity1, Ausgemustert = true });
        transaction.Commit();
    }

    [Fact]
    public async Task TaetigkeitsauswahlFaelltNurBeiNichtAngelegterListeZurueck()
    {
        await CreateMaintenance(_equipment1, new DateOnly(2020, 1, 1), "Kette", "");
        var result = await _service.Laden(new(null, null));
        Assert.Equal("Kette", Assert.Single(result.Taetigkeiten));
        using var db = OeffneDatenbank.Ausfuehren(_database);
        Assert.Null(AuswahllisteEinstellung.Laden(db, Auswahlliste.Taetigkeit));
        using var command = db.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Einstellung';";
        Assert.Equal(0L, command.ExecuteScalar());
    }

    [Fact]
    public async Task ExplizitLeereTaetigkeitsauswahlBleibtLeer()
    {
        await CreateMaintenance(_equipment1, new DateOnly(2020, 1, 1), "Kette", "");
        using (var db = OeffneDatenbank.Ausfuehren(_database))
            AuswahllisteEinstellung.Speichern(db, null, Auswahlliste.Taetigkeit, []);
        var result = await _service.Laden(new(null, null));
        Assert.Empty(result.Taetigkeiten);
        using var verify = OeffneDatenbank.Ausfuehren(_database);
        Assert.Empty(AuswahllisteEinstellung.Laden(verify, Auswahlliste.Taetigkeit)!);
    }

    [Fact]
    public async Task StaleCasVerweigertMutationUndErhaeltBestand()
    {
        var old = await _service.Laden(new(null, null));
        await CreateMaintenance(_equipment1, new DateOnly(2026, 1, 1), "Service", "old");
        var error = await Assert.ThrowsAsync<StammdatenException>(() => _service.WartungAnlegen(new(old.Bestand.Version,
            new DateOnly(2026, 2, 1), _equipment1, "Other", "")));
        Assert.Equal(StammdatenFehlerart.Konflikt, error.Art);
        using var db = OeffneDatenbank.Ausfuehren(_database);
        Assert.Single(WartungenTabelle.Laden(db));
    }

    [Fact]
    public async Task UngueltigeEingabenWerdenAbgelehntOhneBestandsaenderung()
    {
        var snapshot = (await _service.Laden(new(null, null))).Bestand;
        var requests = new[] {
            new WartungSpeichernAnfrage(snapshot.Version, DateOnly.FromDateTime(DateTime.Today).AddDays(1), _equipment1, "X", ""),
            new WartungSpeichernAnfrage(snapshot.Version, DateOnly.FromDateTime(DateTime.Today), 999999, "X", ""),
            new WartungSpeichernAnfrage(snapshot.Version, DateOnly.FromDateTime(DateTime.Today), null, "X", ""),
            new WartungSpeichernAnfrage(snapshot.Version, DateOnly.FromDateTime(DateTime.Today), _equipment1, " Tune ", ""),
            new WartungSpeichernAnfrage(snapshot.Version, DateOnly.FromDateTime(DateTime.Today), _equipment1, "tune", "")
        };
        await _service.WartungAnlegen(requests[3]);
        foreach (var request in requests.Take(3).Append(requests[4]))
        {
            var fresh = (await _service.Laden(new(null, null))).Bestand;
            var call = request with { Version = fresh.Version };
            var error = await Assert.ThrowsAsync<StammdatenException>(() => _service.WartungAnlegen(call));
            var expected = call.EquipmentId == 999999 ? StammdatenFehlerart.NichtGefunden : StammdatenFehlerart.Ungueltig;
            Assert.Equal(expected, error.Art);
            Assert.Equal(fresh.Version, (await _service.Laden(new(null, null))).Bestand.Version);
        }
        var current = (await _service.Laden(new(null, null))).Bestand;
        Assert.Single(current.Eintraege);
        Assert.Equal(current.Version, (await _service.Laden(new(_activity1, null))).Bestand.Version);
        var alarmError = await Assert.ThrowsAsync<StammdatenException>(() => _service.AlarmAnlegen(new(current.Version, _equipment1, "Chain", 0, true)));
        Assert.Equal(StammdatenFehlerart.Ungueltig, alarmError.Art);
    }

    [Fact]
    public async Task BulkDeleteMitFehlenderIdRollbacktAlleLoeschungen()
    {
        await CreateMaintenance(_equipment1, new DateOnly(2026, 1, 1), "A", "");
        var snapshot = (await _service.Laden(new(null, null))).Bestand;
        var error = await Assert.ThrowsAsync<StammdatenException>(() => _service.Loeschen(new(snapshot.Version, [snapshot.Eintraege[0].Id, 999999])));
        Assert.Equal(StammdatenFehlerart.NichtGefunden, error.Art);
        Assert.Single((await _service.Laden(new(null, null))).Bestand.Eintraege);
    }

    [Fact]
    public async Task BulkToggleMitNichtAlarmRollbacktUndAlarmselektionSchaltetEinheitlich()
    {
        await CreateMaintenance(_equipment1, new DateOnly(2020, 1, 1), "Service", "");
        await CreateAlarm(_equipment1, "A", true); await CreateAlarm(_equipment2, "B", false);
        var snapshot = (await _service.Laden(new(null, null))).Bestand;
        var alarmIds = snapshot.Eintraege.Where(x => x.Art == Wartungsart.Alarm).Select(x => x.Id).ToArray();
        var bad = await Assert.ThrowsAsync<StammdatenException>(() => _service.AlarmeUmschalten(new(snapshot.Version, [alarmIds[0], snapshot.Eintraege.Single(x => x.Art == Wartungsart.Wartung).Id])));
        Assert.Equal(StammdatenFehlerart.Ungueltig, bad.Art);
        var afterBad = (await _service.Laden(new(null, null))).Bestand;
        Assert.True(afterBad.Eintraege.Single(x => x.Id == alarmIds[0]).Aktiv);
        var off = await _service.AlarmeUmschalten(new(afterBad.Version, alarmIds));
        Assert.All(off.Eintraege.Where(x => x.Art == Wartungsart.Alarm), x => Assert.False(x.Aktiv));
        var on = await _service.AlarmeUmschalten(new(off.Version, alarmIds));
        Assert.All(on.Eintraege.Where(x => x.Art == Wartungsart.Alarm), x => Assert.True(x.Aktiv));
    }

    [Fact]
    public async Task AlarmEditErhaeltLegacyDatumUndKommentar()
    {
        var id = await Seed(new Wartung { Datum = new DateOnly(2019, 4, 3), EquipmentId = _equipment1, Taetigkeit = "Old", Kommentar = "legacy\r\ntext", Typ = WartungsTyp.Alarm, AlarmKm = 400, Aktiv = true });
        var snapshot = (await _service.Laden(new(null, null))).Bestand;
        var updated = await _service.AlarmAendern(id, new(snapshot.Version, _equipment1, "New", 500, false));
        var row = Assert.Single(updated.Eintraege);
        Assert.Equal(new DateOnly(2019, 4, 3), row.Datum); Assert.Equal("legacy\r\ntext", row.Kommentar);
        Assert.Equal("New", row.Taetigkeit); Assert.Equal(500, row.AlarmKm); Assert.False(row.Aktiv);
    }

    [Fact]
    public async Task WartungEditErhaeltAlarmfelderUndMehrzeiligerKommentarRoundtript()
    {
        var id = await Seed(new Wartung { Datum = new DateOnly(2020, 1, 1), EquipmentId = _equipment1, Taetigkeit = "Old", Kommentar = "legacy", Typ = WartungsTyp.Wartung, AlarmKm = 17, Aktiv = false });
        var snapshot = (await _service.Laden(new(null, null))).Bestand;
        var result = await _service.WartungAendern(id, new(snapshot.Version, new DateOnly(2021, 2, 3), _equipment1, "New", "line1\r\nline2\tend"));
        var item = Assert.Single(result.Eintraege);
        Assert.Equal(Wartungsart.Wartung, item.Art); Assert.Equal(17, item.AlarmKm); Assert.False(item.Aktiv);
        Assert.Equal("line1\r\nline2\tend", item.Kommentar);
    }

    [Fact]
    public async Task FilterVersionBleibtUngefiltertUndProjektionenNutzenCoreBerechnung()
    {
        await CreateMaintenance(_equipment1, new DateOnly(2020, 1, 1), "Kette", "alt");
        await CreateMaintenance(_equipment1, new DateOnly(2021, 1, 1), "Kette", "neu");
        await CreateAlarm(_equipment1, "Kette", true);
        var all = await _service.Laden(new(null, null));
        var filtered = await _service.Laden(new(_activity1, _equipment1));
        Assert.Equal(all.Bestand.Version, filtered.Bestand.Version);
        Assert.Equal("Rad A", filtered.Wartungszeilen[0].Rad);
        Assert.Equal(366, filtered.Wartungszeilen[0].TageSeitLetzter);
        Assert.Equal(0, filtered.Wartungszeilen[0].KmSeitLetzter);
        Assert.Single(filtered.Alarmzeilen);
        Assert.Equal(5000, filtered.Alarmzeilen[0].IntervallKm);
    }

    [Fact]
    public async Task RetiredEquipmentDarfNurBeimBearbeitenSeinerAltenEintraegeBleiben()
    {
        var snapshot = (await _service.Laden(new(null, null))).Bestand;
        var denied = await Assert.ThrowsAsync<StammdatenException>(() => _service.WartungAnlegen(new(snapshot.Version, DateOnly.FromDateTime(DateTime.Today), _retired, "A", "")));
        Assert.Equal(StammdatenFehlerart.Ungueltig, denied.Art);
        var id = await Seed(new Wartung { Datum = new DateOnly(2020, 1, 1), EquipmentId = _retired, Taetigkeit = "Existing", Typ = WartungsTyp.Wartung });
        snapshot = (await _service.Laden(new(null, null))).Bestand;
        await _service.WartungAendern(id, new(snapshot.Version, new DateOnly(2021, 1, 1), _retired, "Existing", "ok"));
        long secondRetired;
        using (var db = OeffneDatenbank.Ausfuehren(_database))
        using (var tx = db.BeginTransaction())
        {
            secondRetired = EquipmentsTabelle.Einfuegen(db, tx, new Equipment { Name = "Alt 2", AktivitaetId = _activity1, Ausgemustert = true });
            tx.Commit();
        }
        snapshot = (await _service.Laden(new(null, null))).Bestand;
        var rejected = await Assert.ThrowsAsync<StammdatenException>(() => _service.WartungAendern(id, new(snapshot.Version, new DateOnly(2021, 1, 1), secondRetired, "Existing", "ok")));
        Assert.Equal(StammdatenFehlerart.Ungueltig, rejected.Art);
        snapshot = (await _service.Laden(new(null, null))).Bestand;
        var allowed = await _service.WartungAendern(id, new(snapshot.Version, new DateOnly(2021, 1, 1), _equipment1, "Existing", "ok"));
        Assert.Equal(_equipment1, allowed.Eintraege.Single(x => x.Id == id).EquipmentId);
    }

    private async Task<long> CreateMaintenance(long equipmentId, DateOnly date, string task, string comment)
    {
        var version = (await _service.Laden(new(null, null))).Bestand.Version;
        var result = await _service.WartungAnlegen(new(version, date, equipmentId, task, comment));
        return result.Eintraege.Single(x => x.Taetigkeit == task && x.Art == Wartungsart.Wartung && x.Datum == date && x.EquipmentId == equipmentId).Id;
    }
    private async Task<long> CreateAlarm(long equipmentId, string task, bool active)
    {
        var version = (await _service.Laden(new(null, null))).Bestand.Version;
        var result = await _service.AlarmAnlegen(new(version, equipmentId, task, 5000, active));
        return result.Eintraege.Single(x => x.Taetigkeit == task && x.Art == Wartungsart.Alarm && x.EquipmentId == equipmentId).Id;
    }
    private async Task<long> Seed(Wartung maintenance)
    {
        using var db = OeffneDatenbank.Ausfuehren(_database); using var tx = db.BeginTransaction();
        var id = WartungenTabelle.Einfuegen(db, tx, maintenance); tx.Commit(); return id;
    }
    public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
