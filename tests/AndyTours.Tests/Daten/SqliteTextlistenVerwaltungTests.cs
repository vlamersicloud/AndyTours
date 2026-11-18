using System.Security.Cryptography;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

/// <summary>Isolierte Tests für versionierte, fachlich beschränkte Textlisten.</summary>
public sealed class SqliteTextlistenVerwaltungTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AndyTours-Textlisten-" + Guid.NewGuid().ToString("N"));
    private readonly string _database;
    private readonly SqliteTextlistenVerwaltung _service;
    private readonly long _activity, _equipment;

    public SqliteTextlistenVerwaltungTests()
    {
        Directory.CreateDirectory(_directory);
        _database = Path.Combine(_directory, "andytours.db");
        _service = new SqliteTextlistenVerwaltung(_database);
        using var db = OeffneDatenbank.Ausfuehren(_database);
        using var tx = db.BeginTransaction();
        _activity = AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Rad" });
        _equipment = EquipmentsTabelle.Einfuegen(db, tx, new Equipment { Name = "Bike", AktivitaetId = _activity });
        tx.Commit();
    }

    [Fact]
    public async Task GetFehlendVerwendetFallbackOhneTextlistenEintragZuSchreiben()
    {
        using (var db = OeffneDatenbank.Ausfuehren(_database))
            TrainingseinheitenTabelle.Einfuegen(db, new Trainingseinheit { AktivitaetId = _activity, EquipmentId = _equipment,
                Datum = new DateOnly(2025, 1, 1), Name = "Fahrt", DistanzKm = 1, Trainingsbereich = "GA1" });
        var result = await _service.Laden(Textlistenart.Trainingsbereich);
        Assert.Equal("GA1", Assert.Single(result.Eintraege));
        using var check = OeffneDatenbank.Ausfuehren(_database);
        Assert.Null(AuswahllisteEinstellung.Laden(check, Auswahlliste.Trainingsbereich));
    }

    [Fact]
    public async Task ExplizitLeereListeBleibtLeer()
    {
        using (var db = OeffneDatenbank.Ausfuehren(_database)) AuswahllisteEinstellung.Speichern(db, null, Auswahlliste.Taetigkeit, []);
        Assert.Empty((await _service.Laden(Textlistenart.Taetigkeit)).Eintraege);
    }

    [Fact]
    public async Task StaleVersionWirdAbgewiesen()
    {
        var old = await _service.Laden(Textlistenart.Taetigkeit);
        await _service.Anlegen(Textlistenart.Taetigkeit, new(old.Version, "Kette"));
        var error = await Assert.ThrowsAsync<StammdatenException>(() => _service.Anlegen(Textlistenart.Taetigkeit, new(old.Version, "Bremse")));
        Assert.Equal(StammdatenFehlerart.Konflikt, error.Art);
    }

    [Fact]
    public async Task GetrimmteUnicodeCaseDuplikateWerdenAbgelehnt()
    {
        var loaded = await _service.Laden(Textlistenart.Taetigkeit);
        var created = await _service.Anlegen(Textlistenart.Taetigkeit, new(loaded.Version, "  Dämpfer  "));
        var error = await Assert.ThrowsAsync<StammdatenException>(() => _service.Anlegen(Textlistenart.Taetigkeit, new(created.Version, "dÄMPFER")));
        Assert.Equal(StammdatenFehlerart.Ungueltig, error.Art);
    }

    [Fact]
    public async Task UmbenennenKaskadiertTrainingsbereichUndCaseOnlyExakt()
    {
        using (var db = OeffneDatenbank.Ausfuehren(_database))
            TrainingseinheitenTabelle.Einfuegen(db, new Trainingseinheit { AktivitaetId = _activity, EquipmentId = _equipment,
                Datum = new DateOnly(2025, 1, 1), Name = "Fahrt", DistanzKm = 1, Trainingsbereich = "GA1" });
        var list = await _service.Laden(Textlistenart.Trainingsbereich);
        var usage = await _service.NutzungLesen(Textlistenart.Trainingsbereich, list.Version, "GA1");
        await _service.Umbenennen(Textlistenart.Trainingsbereich, new(list.Version, "GA1", "ga1", usage.VerwendungsVersion));
        using var verify = OeffneDatenbank.Ausfuehren(_database);
        Assert.Equal("ga1", Assert.Single(TrainingseinheitenTabelle.Laden(verify)).Trainingsbereich);
    }

    [Fact]
    public async Task UmbenennenTaetigkeitAktualisiertWartungenUndAlarme()
    {
        using (var db = OeffneDatenbank.Ausfuehren(_database))
        {
            WartungenTabelle.Einfuegen(db, new Wartung { Datum = new DateOnly(2020, 1, 1), EquipmentId = _equipment, Taetigkeit = "Kette", Typ = WartungsTyp.Wartung });
            WartungenTabelle.Einfuegen(db, new Wartung { Datum = new DateOnly(2020, 1, 1), EquipmentId = _equipment, Taetigkeit = "Kette", Typ = WartungsTyp.Alarm, AlarmKm = 500, Aktiv = true });
        }
        var list = await _service.Laden(Textlistenart.Taetigkeit);
        var usage = await _service.NutzungLesen(Textlistenart.Taetigkeit, list.Version, "Kette");
        Assert.Equal(1, usage.Wartungen);
        Assert.Equal(1, usage.Alarme);
        var result = await _service.Umbenennen(Textlistenart.Taetigkeit, new(list.Version, "Kette", "Antrieb", usage.VerwendungsVersion));
        using var verify = OeffneDatenbank.Ausfuehren(_database);
        Assert.All(WartungenTabelle.Laden(verify), w => Assert.Equal("Antrieb", w.Taetigkeit));
        Assert.Equal("Antrieb", Assert.Single(result.Eintraege));
    }

    [Fact]
    public async Task UsagePreviewZaehltTourenUndReadOnlyMissingDatabaseBleibtFehlend()
    {
        var dbPath = Path.Combine(_directory, "missing.db");
        var missingStore = new SqliteTextlistenVerwaltung(dbPath);
        await Assert.ThrowsAsync<SqliteException>(() =>
            missingStore.NutzungLesen(Textlistenart.Trainingsbereich, new string('0', 64), "GA1"));
        Assert.False(File.Exists(dbPath));

        using (var db = OeffneDatenbank.Ausfuehren(_database))
        {
            TrainingseinheitenTabelle.Einfuegen(db, new Trainingseinheit { AktivitaetId = _activity, EquipmentId = _equipment,
                Datum = new DateOnly(2025, 1, 1), Name = "A", DistanzKm = 1, Trainingsbereich = "GA1" });
            TrainingseinheitenTabelle.Einfuegen(db, new Trainingseinheit { AktivitaetId = _activity, EquipmentId = _equipment,
                Datum = new DateOnly(2025, 1, 2), Name = "B", DistanzKm = 1, Trainingsbereich = "ga1" });
        }
        var list = await _service.Laden(Textlistenart.Trainingsbereich);
        var before = SHA256.HashData(File.ReadAllBytes(_database));
        var usage = await _service.NutzungLesen(Textlistenart.Trainingsbereich, list.Version, "GA1");
        var after = SHA256.HashData(File.ReadAllBytes(_database));
        Assert.Equal(before, after);
        Assert.Equal(2, usage.Touren);
        Assert.Equal(0, usage.Wartungen);
        Assert.Equal(0, usage.Alarme);
    }

    [Fact]
    public async Task UnicodeCaseAndWhitespaceUsageRenamesOnlyCoreMatchedIds()
    {
        using (var db = OeffneDatenbank.Ausfuehren(_database))
        {
            AuswahllisteEinstellung.Speichern(db, null, Auswahlliste.Trainingsbereich, ["Ä"]);
            TrainingseinheitenTabelle.Einfuegen(db, new Trainingseinheit { AktivitaetId = _activity, EquipmentId = _equipment,
                Datum = new DateOnly(2025, 1, 1), Name = "Matched", DistanzKm = 1, Trainingsbereich = "\u00A0Ä\u00A0" });
            TrainingseinheitenTabelle.Einfuegen(db, new Trainingseinheit { AktivitaetId = _activity, EquipmentId = _equipment,
                Datum = new DateOnly(2025, 1, 2), Name = "Unrelated", DistanzKm = 1, Trainingsbereich = "äx" });
        }
        var list = await _service.Laden(Textlistenart.Trainingsbereich);
        var usage = await _service.NutzungLesen(Textlistenart.Trainingsbereich, list.Version, "Ä");
        Assert.Equal(1, usage.Touren);
        await _service.Umbenennen(Textlistenart.Trainingsbereich,
            new(list.Version, "Ä", "Zone", usage.VerwendungsVersion));
        using var verify = OeffneDatenbank.Ausfuehren(_database);
        var units = TrainingseinheitenTabelle.Laden(verify);
        Assert.Equal("Zone", units.Single(x => x.Name == "Matched").Trainingsbereich);
        Assert.Equal("äx", units.Single(x => x.Name == "Unrelated").Trainingsbereich);
    }

    [Fact]
    public async Task SameCountButDifferentAffectedRowInvalidatesRenameStamp()
    {
        using (var db = OeffneDatenbank.Ausfuehren(_database))
        {
            AuswahllisteEinstellung.Speichern(db, null, Auswahlliste.Trainingsbereich, ["GA1"]);
            TrainingseinheitenTabelle.Einfuegen(db, new Trainingseinheit { AktivitaetId = _activity, EquipmentId = _equipment,
                Datum = new DateOnly(2025, 1, 1), Name = "A", DistanzKm = 1, Trainingsbereich = "GA1" });
        }
        var list = await _service.Laden(Textlistenart.Trainingsbereich);
        var usage = await _service.NutzungLesen(Textlistenart.Trainingsbereich, list.Version, "GA1");
        using (var db = OeffneDatenbank.Ausfuehren(_database))
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "UPDATE Trainingseinheit SET Trainingsbereich = 'Anders' WHERE Id = (SELECT MIN(Id) FROM Trainingseinheit);";
            cmd.ExecuteNonQuery();
            TrainingseinheitenTabelle.Einfuegen(db, new Trainingseinheit { AktivitaetId = _activity, EquipmentId = _equipment,
                Datum = new DateOnly(2025, 1, 2), Name = "B", DistanzKm = 1, Trainingsbereich = "GA1" });
        }
        var ex = await Assert.ThrowsAsync<StammdatenException>(() =>
            _service.Umbenennen(Textlistenart.Trainingsbereich, new(list.Version, "GA1", "Zone", usage.VerwendungsVersion)));
        Assert.Equal(StammdatenFehlerart.Konflikt, ex.Art);
        using var verify = OeffneDatenbank.Ausfuehren(_database);
        Assert.Equal("Anders", TrainingseinheitenTabelle.Laden(verify).Single(x => x.Name == "A").Trainingsbereich);
    }

    [Fact]
    public async Task LoeschenVerwendeteListeBlockiertUndLetzterEintragMaterialisiertLeer()
    {
        using (var db = OeffneDatenbank.Ausfuehren(_database))
            AuswahllisteEinstellung.Speichern(db, null, Auswahlliste.Taetigkeit, ["Kette"]);
        using (var db = OeffneDatenbank.Ausfuehren(_database))
            WartungenTabelle.Einfuegen(db, new Wartung { Datum = new DateOnly(2020, 1, 1), EquipmentId = _equipment, Taetigkeit = "Kette", Typ = WartungsTyp.Alarm, AlarmKm = 500 });
        var list = await _service.Laden(Textlistenart.Taetigkeit);
        var blocked = await Assert.ThrowsAsync<StammdatenException>(() => _service.Loeschen(Textlistenart.Taetigkeit, new(list.Version, "Kette")));
        Assert.Equal(StammdatenFehlerart.Konflikt, blocked.Art);
        using (var db = OeffneDatenbank.Ausfuehren(_database)) WartungenTabelle.Loeschen(db, WartungenTabelle.Laden(db).Select(w => w.Id).ToArray());
        list = await _service.Laden(Textlistenart.Taetigkeit);
        var empty = await _service.Loeschen(Textlistenart.Taetigkeit, new(list.Version, "Kette"));
        Assert.Empty(empty.Eintraege);
        using var verify = OeffneDatenbank.Ausfuehren(_database);
        Assert.Empty(AuswahllisteEinstellung.Laden(verify, Auswahlliste.Taetigkeit)!);
    }

    [Fact]
    public async Task ReihenfolgeIstExaktePermutationUndBewahrtKanonischeNamen()
    {
        using (var db = OeffneDatenbank.Ausfuehren(_database)) AuswahllisteEinstellung.Speichern(db, null, Auswahlliste.Taetigkeit, ["Kette", "Bremse"]);
        var list = await _service.Laden(Textlistenart.Taetigkeit);
        var result = await _service.SetzeReihenfolge(Textlistenart.Taetigkeit, new(list.Version, ["bReMsE", "KETTE"]));
        Assert.Equal("Bremse", result.Eintraege[0]);
        Assert.Equal("Kette", result.Eintraege[1]);
        var missing = await Assert.ThrowsAsync<StammdatenException>(() => _service.SetzeReihenfolge(Textlistenart.Taetigkeit, new(result.Version, ["Kette"])));
        Assert.Equal(StammdatenFehlerart.Ungueltig, missing.Art);
        var duplicate = await Assert.ThrowsAsync<StammdatenException>(() => _service.SetzeReihenfolge(Textlistenart.Taetigkeit, new(result.Version, ["Kette", "kette"])));
        Assert.Equal(StammdatenFehlerart.Ungueltig, duplicate.Art);
    }

    [Fact]
    public async Task NichtUnterstuetzteEnumWirdAbgewiesen()
    {
        var error = await Assert.ThrowsAsync<StammdatenException>(() => _service.Laden((Textlistenart)999));
        Assert.Equal(StammdatenFehlerart.Ungueltig, error.Art);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
