using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Core.Berechnungen;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

/// <summary>Isolierte Regressionen für die SQLite-Equipmentverwaltung.</summary>
public sealed class SqliteEquipmentsVerwaltungTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AndyTours-EquipmentApi-" + Guid.NewGuid().ToString("N"));
    private readonly string _db;
    private readonly SqliteEquipmentsVerwaltung _sut;
    private readonly SqliteTourenSpeicher _tours;
    private readonly long _activity1, _activity2;

    public SqliteEquipmentsVerwaltungTests()
    {
        Directory.CreateDirectory(_dir); _db = Path.Combine(_dir, "andytours.db");
        _sut = new(_db); _tours = new(_db);
        using var c = OeffneDatenbank.Ausfuehren(_db); using var tx = c.BeginTransaction();
        _activity1 = AktivitaetenTabelle.Einfuegen(c, tx, new Aktivitaet { Name = "Rad", IstHaupt = true });
        _activity2 = AktivitaetenTabelle.Einfuegen(c, tx, new Aktivitaet { Name = "Lauf" }); tx.Commit();
    }

    [Fact] public async Task VeralteteVersionVerweigertMutation()
    {
        var old = await _sut.Laden(); await Add("Bike", _activity1);
        var ex = await Assert.ThrowsAsync<StammdatenException>(() => _sut.Anlegen(Request(old.Version, Values("Nope", _activity1))));
        Assert.Equal(StammdatenFehlerart.Konflikt, ex.Art);
        Assert.DoesNotContain((await _sut.Laden()).Eintraege, x => x.Name == "Nope");
    }

    [Fact] public async Task StandardTransferBleibtAufAktivitaetBegrenzt()
    {
        var first = await Add("A", _activity1, standard: true); await Add("B", _activity2, standard: true);
        await Add("C", _activity1, standard: true);
        using var c = OeffneDatenbank.Ausfuehren(_db); var all = EquipmentsTabelle.Laden(c);
        Assert.False(all.Single(x => x.Id == first).IstStandard); Assert.True(all.Single(x => x.Name == "B").IstStandard);
        Assert.True(all.Single(x => x.Name == "C").IstStandard);
    }

    [Fact] public async Task AusmusternEntferntStandard()
    {
        var id = await Add("Bike", _activity1, standard: true);
        await Change(id, Values("Bike", _activity1, standard: true, retired: true));
        Assert.False((await _sut.Laden()).Eintraege.Single(x => x.Id == id).IstStandard);
    }

    [Fact] public async Task UngueltigeAktivitaetDuplikatUndDistanzLassenBestandUnveraendert()
    {
        var id = await Add("A", _activity1); var before = await _sut.Laden();
        await Assert.ThrowsAsync<StammdatenException>(() => _sut.Anlegen(Request(before.Version, Values("B", 999999))));
        Assert.Equal(before.Version, (await _sut.Laden()).Version);
        before = await _sut.Laden(); await Assert.ThrowsAsync<StammdatenException>(() => _sut.Anlegen(Request(before.Version, Values(" a ", _activity1))));
        Assert.Equal(before.Version, (await _sut.Laden()).Version);
        before = await _sut.Laden(); await Assert.ThrowsAsync<StammdatenException>(() => _sut.Aendern(id, Request(before.Version, Values("A", _activity1, km: double.NaN))));
        Assert.Equal(before.Version, (await _sut.Laden()).Version);
        before = await _sut.Laden(); await Assert.ThrowsAsync<StammdatenException>(() => _sut.Aendern(id, Request(before.Version, Values("A", _activity1, km: -1))));
        before = await _sut.Laden(); await Assert.ThrowsAsync<StammdatenException>(() => _sut.Aendern(id, Request(before.Version, Values("A", _activity1, km: double.PositiveInfinity))));
        using var c = OeffneDatenbank.Ausfuehren(_db); Assert.Equal("A", EquipmentsTabelle.Laden(c).Single().Name);
    }

    [Fact] public async Task LoeschenBeiTourOderWartungGesperrt()
    {
        var id = await Add("Bike", _activity1); var vals = TourenMapper.ToAntwort(new Trainingseinheit { AktivitaetId = _activity1,
            Datum = new DateOnly(2026, 1, 1), Name = "Tour", DistanzKm = 2, Fahrzeit = TimeSpan.FromMinutes(10), EquipmentId = id }).Werte;
        await _tours.Anlegen(vals);
        var version = (await _sut.Laden()).Version;
        var ex = await Assert.ThrowsAsync<StammdatenException>(() => _sut.Loeschen(id, version));
        Assert.Equal(StammdatenFehlerart.Konflikt, ex.Art); Assert.Single((await _sut.Laden()).Eintraege);

        var maintenanceId = await Add("Maintenance", _activity1);
        using (var db = OeffneDatenbank.Ausfuehren(_db))
        {
            using var tx = db.BeginTransaction();
            WartungenTabelle.Speichern(db, tx, [new Wartung { Id = 1, EquipmentId = maintenanceId,
                Datum = new DateOnly(2026, 1, 2), Taetigkeit = "Kette" }]);
            tx.Commit();
        }
        version = (await _sut.Laden()).Version;
        ex = await Assert.ThrowsAsync<StammdatenException>(() => _sut.Loeschen(maintenanceId, version));
        Assert.Equal(StammdatenFehlerart.Konflikt, ex.Art);
        Assert.Contains((await _sut.Laden()).Eintraege, e => e.Id == maintenanceId);
        using (var db = OeffneDatenbank.Ausfuehren(_db))
        {
            Assert.Contains(WartungenTabelle.Laden(db), w => w.EquipmentId == maintenanceId);
        }
    }

    [Fact] public async Task RawLegacyMappingIsPreservedAndOnlyRemovalAllowed()
    {
        var id = await Add("Old", _activity1); const string raw = "Bike1;  Sensor:abc  ";
        using (var c = OeffneDatenbank.Ausfuehren(_db)) { using var tx = c.BeginTransaction(); var e = EquipmentsTabelle.Laden(c).Single(); e.Geraetezuordnung = raw; EquipmentsTabelle.Aendern(c, tx, e); tx.Commit(); }
        var snap = await _sut.Laden(); await Change(id, Values("Old", _activity1, mapping: raw));
        using (var c = OeffneDatenbank.Ausfuehren(_db)) Assert.Equal(raw, c.QueryMapping());
        snap = await _sut.Laden(); await Change(id, Values("Old", _activity1, mapping: "Bike1"));
        using (var c = OeffneDatenbank.Ausfuehren(_db)) Assert.Equal("Bike1", c.QueryMapping());
        snap = await _sut.Laden(); var fail = await Assert.ThrowsAsync<StammdatenException>(() => _sut.Aendern(id, Request(snap.Version, Values("Old", _activity1, mapping: "Rad:new"))));
        Assert.Equal(StammdatenFehlerart.Ungueltig, fail.Art);
    }

    [Fact] public async Task KategorienWerdenKanonisiertUndVerwaisteAltwerteBewahrt()
    {
        using (var c = OeffneDatenbank.Ausfuehren(_db))
        using (var tx = c.BeginTransaction())
        {
            KategorienTabelle.Einfuegen(c, tx, new Kategorie { Name = "Tour" });
            tx.Commit();
        }
        var id = await Add("Legacy", _activity1);
        await Change(id, Values("Legacy", _activity1, category: " tour "));
        Assert.Equal("Tour", (await _sut.Laden()).Eintraege.Single().StandardKategorie);
        using (var c = OeffneDatenbank.Ausfuehren(_db))
        {
            using var tx = c.BeginTransaction();
            var equipment = EquipmentsTabelle.Laden(c).Single(e => e.Id == id);
            equipment.StandardKategorie = "Unknown legacy";
            EquipmentsTabelle.Aendern(c, tx, equipment);
            tx.Commit();
        }
        await Change(id, Values("Legacy", _activity1, category: "Unknown legacy"));
        Assert.Equal("Unknown legacy", (await _sut.Laden()).Eintraege.Single().StandardKategorie);
        var snap = await _sut.Laden();
        var ex = await Assert.ThrowsAsync<StammdatenException>(() => _sut.Aendern(id, Request(snap.Version, Values("Legacy", _activity1, category: "Unknown new"))));
        Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
        Assert.Equal("Unknown legacy", (await _sut.Laden()).Eintraege.Single().StandardKategorie);
    }

    [Fact] public async Task VollstaendigeWerteWerdenInAntwortUndDatenbankErhalten()
    {
        using (var c = OeffneDatenbank.Ausfuehren(_db))
        using (var tx = c.BeginTransaction())
        {
            KategorienTabelle.Einfuegen(c, tx, new Kategorie { Name = "Tour" });
            tx.Commit();
        }
        var req = Values("  Trek  ", _activity1, standard: true, km: 1234.5, category: " tour ")
            with { Anschaffungsdatum = new DateOnly(2020, 2, 29) };
        var created = await _sut.Anlegen(Request((await _sut.Laden()).Version, req));
        var entry = created.Eintraege.Single();
        Assert.Equal("Trek", entry.Name); Assert.Equal(_activity1, entry.AktivitaetId);
        Assert.Equal(string.Empty, entry.Geraetezuordnung); Assert.Equal("Tour", entry.StandardKategorie);
        Assert.True(entry.IstStandard); Assert.Equal(new DateOnly(2020, 2, 29), entry.Anschaffungsdatum);
        Assert.Equal(1234.5, entry.AnfangsdistanzKm);
        using (var c = OeffneDatenbank.Ausfuehren(_db))
        {
            var saved = EquipmentsTabelle.Laden(c).Single(e => e.Id == entry.Id);
            Assert.Equal(entry.Name, saved.Name); Assert.Equal(entry.AktivitaetId, saved.AktivitaetId);
            Assert.Equal(entry.Geraetezuordnung, saved.Geraetezuordnung); Assert.Equal(entry.StandardKategorie, saved.StandardKategorie);
            Assert.Equal(entry.IstStandard, saved.IstStandard); Assert.Equal(entry.Anschaffungsdatum, saved.Anschaffungsdatum);
            Assert.Equal(entry.AnfangsdistanzKm, saved.AnfangsdistanzKm);
        }
        var changed = Values("Trek II", _activity2, km: 9, category: "Tour") with { Anschaffungsdatum = new DateOnly(2021, 3, 1) };
        var updated = await _sut.Aendern(entry.Id, Request(created.Version, changed));
        var answer = updated.Eintraege.Single(e => e.Id == entry.Id);
        Assert.Equal("Trek II", answer.Name); Assert.Equal(_activity2, answer.AktivitaetId);
        Assert.Equal(new DateOnly(2021, 3, 1), answer.Anschaffungsdatum); Assert.Equal(9, answer.AnfangsdistanzKm);
        Assert.Equal("Tour", answer.StandardKategorie);
        using (var c = OeffneDatenbank.Ausfuehren(_db))
        {
            var saved = EquipmentsTabelle.Laden(c).Single(e => e.Id == entry.Id);
            Assert.Equal(answer.Name, saved.Name);
            Assert.Equal(answer.AktivitaetId, saved.AktivitaetId);
            Assert.Equal(answer.Anschaffungsdatum, saved.Anschaffungsdatum);
            Assert.Equal(answer.AnfangsdistanzKm, saved.AnfangsdistanzKm);
            Assert.Equal(answer.StandardKategorie, saved.StandardKategorie);
            Assert.Equal(answer.Geraetezuordnung, saved.Geraetezuordnung);
        }
    }

    private async Task<long> Add(string name, long? activity, bool standard = false) =>
        (await _sut.Anlegen(Request((await _sut.Laden()).Version, Values(name, activity, standard)))).Eintraege.Single(e => e.Name == name).Id;
    private async Task Change(long id, EquipmentWerte w) => await _sut.Aendern(id, Request((await _sut.Laden()).Version, w));
    private static EquipmentSpeichernAnfrage Request(string v, EquipmentWerte w) => new(v, w);
    private static EquipmentWerte Values(string n, long? a, bool standard = false, bool retired = false, double km = 0, string mapping = "", string category = "") => new(n, a, mapping, category, standard, retired, null, km);
    public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
}

internal static class EquipmentTestSql
{
    public static string QueryMapping(this SqliteConnection c) { using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT Geraetezuordnung FROM Equipment"; return (string)cmd.ExecuteScalar()!; }
}
