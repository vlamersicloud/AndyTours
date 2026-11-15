using AndyTours.Application.Auswertung;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

/// <summary>Regressionen für serverseitige Tourenfilter und Count-vor-Paging.</summary>
public sealed class SqliteTourenFilterTests : IDisposable
{
    private readonly string _ordner = Path.Combine(Path.GetTempPath(), "AndyTours-Tourenfilter-" + Guid.NewGuid().ToString("N"));
    private readonly string _datenbank;
    private readonly SqliteTourenSpeicher _touren;
    private readonly SqliteAktivitaetsauswertung _auswertung;
    private long _aktivitaetA;
    private long _aktivitaetB;
    private long _kategorie;

    public SqliteTourenFilterTests()
    {
        Directory.CreateDirectory(_ordner);
        _datenbank = Path.Combine(_ordner, "andytours.db");
        _touren = new SqliteTourenSpeicher(_datenbank);
        _auswertung = new SqliteAktivitaetsauswertung(_datenbank);
        using var c = OeffneDatenbank.Ausfuehren(_datenbank);
        using var tx = c.BeginTransaction();
        _aktivitaetA = AktivitaetenTabelle.Einfuegen(c, tx, new Aktivitaet { Name = "Rad", IstHaupt = true });
        _aktivitaetB = AktivitaetenTabelle.Einfuegen(c, tx, new Aktivitaet { Name = "Laufen" });
        _kategorie = KategorienTabelle.Einfuegen(c, tx, new Kategorie { Name = "Training" });
        tx.Commit();
    }

    [Fact]
    public async Task AktivitaetUndKategorieWerdenGemeinsamGefiltert()
    {
        await LegeTourAn(_aktivitaetA, [_kategorie], "Passend");
        await LegeTourAn(_aktivitaetA, [], "OhneKategorie");
        await LegeTourAn(_aktivitaetB, [_kategorie], "AndereAktivitaet");
        var liste = await _touren.ListeGefiltert(0, 20,
            new TourenListenfilter(null, null, _aktivitaetA, _kategorie));
        var tour = Assert.Single(liste.Eintraege);
        Assert.Equal("Passend", tour.Werte.Name);
    }

    [Fact]
    public async Task CountWirdVorPagingBerechnet()
    {
        await LegeTourAn(_aktivitaetA, [_kategorie], "Eins");
        await LegeTourAn(_aktivitaetA, [_kategorie], "Zwei");
        var liste = await _touren.ListeGefiltert(1, 1,
            new TourenListenfilter(null, null, _aktivitaetA, _kategorie));
        Assert.Equal(2, liste.Gesamt);
        Assert.Equal(1, liste.Start);
        Assert.Single(liste.Eintraege);
    }

    [Fact]
    public async Task StatistikUndTourenlisteHabenMitGleichenFilternDieselbeAnzahl()
    {
        await LegeTourAn(_aktivitaetA, [_kategorie], "Eins");
        await LegeTourAn(_aktivitaetA, [_kategorie], "Zwei");
        await LegeTourAn(_aktivitaetB, [_kategorie], "Andere");
        var von = new DateOnly(2026, 1, 1);
        var bis = new DateOnly(2026, 12, 31);
        var liste = await _touren.ListeGefiltert(0, 50, new TourenListenfilter(von, bis, _aktivitaetA, _kategorie));
        var statistik = await _auswertung.Laden(new AktivitaetsauswertungAnfrage(
            von, bis, Auswertungsraster.Jahr, _aktivitaetA, _kategorie));
        Assert.Equal(statistik.Anzahl, liste.Gesamt);
    }

    [Fact]
    public async Task UngueltigeAktivitaetsOderKategorieIdWirdAbgewiesen()
    {
        var filter = new TourenListenfilter(null, null, 0, null);
        await Assert.ThrowsAsync<UngueltigeTourException>(async () => await _touren.ListeGefiltert(0, 10, filter));
        filter = new TourenListenfilter(null, null, null, -1);
        await Assert.ThrowsAsync<UngueltigeTourException>(async () => await _touren.ListeGefiltert(0, 10, filter));
    }

    private async Task LegeTourAn(long aktivitaetId, long[] kategorien, string name)
    {
        var werte = TourenMapper.ToAntwort(new Trainingseinheit
        {
            AktivitaetId = aktivitaetId,
            Datum = new DateOnly(2026, 4, 12),
            Name = name,
            DistanzKm = 12,
            Fahrzeit = TimeSpan.FromMinutes(45)
        }).Werte with { KategorieIds = kategorien };
        await _touren.Anlegen(werte);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_ordner)) Directory.Delete(_ordner, recursive: true);
    }
}
