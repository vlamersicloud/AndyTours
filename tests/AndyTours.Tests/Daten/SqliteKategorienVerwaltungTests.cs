using AndyTours.Application.Stammdaten;
using AndyTours.Core.Modell;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

/// <summary>Isolierte SQLite-Regressionen für Kategorienverwaltung und Referenzübernahme.</summary>
public sealed class SqliteKategorienVerwaltungTests : IDisposable
{
    private readonly string _ordner = Path.Combine(Path.GetTempPath(), "AndyTours-KategorienApi-" + Guid.NewGuid().ToString("N"));
    private readonly string _datenbank;
    private readonly SqliteKategorienVerwaltung _kategorien;
    private readonly SqliteTourenSpeicher _touren;
    private readonly long _aktivitaetId;

    public SqliteKategorienVerwaltungTests()
    {
        Directory.CreateDirectory(_ordner);
        _datenbank = Path.Combine(_ordner, "andytours.db");
        _kategorien = new SqliteKategorienVerwaltung(_datenbank);
        _touren = new SqliteTourenSpeicher(_datenbank);
        using var c = OeffneDatenbank.Ausfuehren(_datenbank);
        using var tx = c.BeginTransaction();
        _aktivitaetId = AktivitaetenTabelle.Einfuegen(c, tx, new Aktivitaet { Name = "Rad", IstHaupt = true });
        tx.Commit();
    }

    [Fact]
    public async Task VeralteteFamilienversionWirdAbgelehnt()
    {
        var first = await LegeKategorieAn("Rad");
        await LegeKategorieAn("Laufen");
        var error = await Assert.ThrowsAsync<StammdatenException>(() =>
            _kategorien.Anlegen(new KategorieSpeichernAnfrage(first.Version, "Schwimmen")));
        Assert.Equal(StammdatenFehlerart.Konflikt, error.Art);
    }

    [Fact]
    public async Task DuplikateWerdenNachTrimUndOhneGrossKleinschreibungErkannt()
    {
        var first = await LegeKategorieAn(" Training ");
        Assert.Equal("Training", Assert.Single(first.Eintraege).Name);
        var error = await Assert.ThrowsAsync<StammdatenException>(() =>
            _kategorien.Anlegen(new KategorieSpeichernAnfrage(first.Version, "  training ")));
        Assert.Equal(StammdatenFehlerart.Ungueltig, error.Art);
    }

    [Fact]
    public async Task UmbenennenAktualisiertEquipmentStandardkategorienReferenz()
    {
        var snapshot = await LegeKategorieAn("Vorher");
        var category = Assert.Single(snapshot.Eintraege);
        FuegeEquipmentHinzu("Sensor", "  vorher ");
        var renamed = await _kategorien.Umbenennen(category.Id,
            new KategorieSpeichernAnfrage(snapshot.Version, "Nachher"));
        Assert.Equal("Nachher", Assert.Single(renamed.Eintraege).Name);
        using var c = OeffneDatenbank.Ausfuehren(_datenbank);
        Assert.Equal("Nachher", Assert.Single(EquipmentsTabelle.Laden(c)).StandardKategorie);
    }

    [Fact]
    public async Task LoeschenWirdBeiTourUndEquipmentStandardverwendungGesperrt()
    {
        var categoryTour = await LegeKategorieAn("Tourenmarke");
        var tourCategory = Assert.Single(categoryTour.Eintraege);
        await LegeTourAn([tourCategory.Id]);
        var usedByTour = await Assert.ThrowsAsync<StammdatenException>(() =>
            _kategorien.Loeschen(tourCategory.Id, categoryTour.Version));
        Assert.Equal(StammdatenFehlerart.Konflikt, usedByTour.Art);

        var categoryEquipment = await LegeKategorieAn("Equipmarke");
        Assert.Equal(2, categoryEquipment.Eintraege.Count);
        var equipmentCategory = categoryEquipment.Eintraege.Single(k => k.Name == "Equipmarke");
        FuegeEquipmentHinzu("Bike", equipmentCategory.Name);
        var usedByEquipment = await Assert.ThrowsAsync<StammdatenException>(() =>
            _kategorien.Loeschen(equipmentCategory.Id, categoryEquipment.Version));
        Assert.Equal(StammdatenFehlerart.Konflikt, usedByEquipment.Art);
    }

    [Fact]
    public async Task MehrereStandardsSindErlaubtUndKoennenEinzelnDeaktiviertWerden()
    {
        var snapshot = await LegeKategorieAn("A");
        snapshot = await _kategorien.Anlegen(new KategorieSpeichernAnfrage(snapshot.Version, "B"));
        var a = snapshot.Eintraege[0];
        var b = snapshot.Eintraege[1];
        snapshot = await _kategorien.SetzeStandard(new KategorieStandardAnfrage(snapshot.Version, a.Id, true));
        snapshot = await _kategorien.SetzeStandard(new KategorieStandardAnfrage(snapshot.Version, b.Id, true));
        snapshot = await _kategorien.SetzeStandard(new KategorieStandardAnfrage(snapshot.Version, a.Id, false));
        Assert.False(snapshot.Eintraege.Single(k => k.Id == a.Id).IstStandard);
        Assert.True(snapshot.Eintraege.Single(k => k.Id == b.Id).IstStandard);
    }

    [Fact]
    public async Task ZusammenfuehrenDedupedTourlinksUndUebertraegtEquipmentUndStandard()
    {
        var snapshot = await LegeKategorieAn("Quelle");
        var source = Assert.Single(snapshot.Eintraege);
        snapshot = await _kategorien.Anlegen(new KategorieSpeichernAnfrage(snapshot.Version, "Ziel"));
        var target = snapshot.Eintraege.Single(k => k.Name == "Ziel");
        snapshot = await _kategorien.SetzeStandard(new KategorieStandardAnfrage(snapshot.Version, source.Id, true));
        source = snapshot.Eintraege.Single(k => k.Id == source.Id);
        target = snapshot.Eintraege.Single(k => k.Id == target.Id);

        FuegeEquipmentHinzu("Device", source.Name);
        var tour = await LegeTourAn([source.Id, target.Id]);
        var merged = await _kategorien.Zusammenfuehren(
            new KategorienZusammenfuehrenAnfrage(snapshot.Version, source.Id, target.Id));

        Assert.DoesNotContain(merged.Eintraege, k => k.Id == source.Id);
        Assert.True(merged.Eintraege.Single(k => k.Id == target.Id).IstStandard);
        using var c = OeffneDatenbank.Ausfuehren(_datenbank);
        var equipment = Assert.Single(EquipmentsTabelle.Laden(c));
        Assert.Equal(target.Name, equipment.StandardKategorie);
        var savedTour = TrainingseinheitenTabelle.Laden(c).Single(t => t.Id == tour);
        Assert.Equal(new[] { target.Id }, savedTour.KategorieIds);
    }

    private async Task<KategorienAntwort> LegeKategorieAn(string name) =>
        await _kategorien.Anlegen(new KategorieSpeichernAnfrage((await _kategorien.Laden()).Version, name));

    private void FuegeEquipmentHinzu(string name, string standardKategorie)
    {
        using var c = OeffneDatenbank.Ausfuehren(_datenbank);
        using var tx = c.BeginTransaction();
        EquipmentsTabelle.Einfuegen(c, tx, new Equipment
        {
            Name = name,
            StandardKategorie = standardKategorie
        });
        tx.Commit();
    }

    private async Task<long> LegeTourAn(long[] kategorieIds)
    {
        var values = TourenMapper.ToAntwort(new Trainingseinheit
        {
            AktivitaetId = _aktivitaetId,
            Datum = new DateOnly(2026, 4, 15),
            Name = "Testtour",
            DistanzKm = 10,
            Fahrzeit = TimeSpan.FromMinutes(30)
        }).Werte with { KategorieIds = kategorieIds };
        return (await _touren.Anlegen(values)).Id;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_ordner)) Directory.Delete(_ordner, recursive: true);
    }
}
