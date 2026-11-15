using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

/// <summary>Regressionstests für die serverseitige Statistik auf einer isolierten SQLite-Datenbank.</summary>
public sealed class AktivitaetsauswertungApiTests : IDisposable
{
    private readonly string _ordner = Path.Combine(Path.GetTempPath(), "AndyTours-AuswertungApi-" + Guid.NewGuid().ToString("N"));
    private readonly string _datenbank;
    private readonly SqliteTourenSpeicher _touren;
    private readonly SqliteAktivitaetsauswertung _auswertung;
    private readonly long _aktivitaetId;

    public AktivitaetsauswertungApiTests()
    {
        Directory.CreateDirectory(_ordner);
        _datenbank = Path.Combine(_ordner, "andytours.db");
        _touren = new SqliteTourenSpeicher(_datenbank);
        _auswertung = new SqliteAktivitaetsauswertung(_datenbank);

        using var verbindung = OeffneDatenbank.Ausfuehren(_datenbank);
        using var transaktion = verbindung.BeginTransaction();
        _aktivitaetId = AktivitaetenTabelle.Einfuegen(verbindung, transaktion,
            new Aktivitaet { Name = "Rad" });
        transaktion.Commit();
    }

    [Fact]
    public async Task Monat_BehaeltLeereZwischenperiodeUndSummiertTouren()
    {
        await LegeTourAn(new DateOnly(2026, 1, 12));
        await LegeTourAn(new DateOnly(2026, 3, 12));

        var ergebnis = await _auswertung.Laden(new AktivitaetsauswertungAnfrage(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), Auswertungsraster.Monat, null, null));

        Assert.Equal(3, ergebnis.Perioden.Count);
        Assert.Equal(0, ergebnis.Perioden[1].Anzahl);
        Assert.Equal(2, ergebnis.Anzahl);
        Assert.Equal(20, ergebnis.DistanzKm);
        Assert.Equal(3600, ergebnis.FahrzeitSekunden);
    }

    [Fact]
    public async Task Zukunftsaktivitaet_WirdNichtInZeitraumsummeAufgenommen()
    {
        var heute = DateOnly.FromDateTime(DateTime.Today);
        await LegeTourAn(heute);
        await LegeTourAn(heute.AddDays(30));

        var ergebnis = await _auswertung.Laden(new AktivitaetsauswertungAnfrage(
            new DateOnly(heute.Year, 1, 1), heute, Auswertungsraster.Monat, null, null));

        Assert.Equal(1, ergebnis.Anzahl);
        Assert.Equal(1, ergebnis.ZukuenftigeAktivitaeten);
    }

    [Fact]
    public async Task Kalenderwoche_UeberJahreswechselVerwendetIsoJahr()
    {
        await LegeTourAn(new DateOnly(2020, 12, 31));
        await LegeTourAn(new DateOnly(2021, 1, 1));

        var ergebnis = await _auswertung.Laden(new AktivitaetsauswertungAnfrage(
            new DateOnly(2020, 12, 28), new DateOnly(2021, 1, 3), Auswertungsraster.Woche, null, null));

        var periode = Assert.Single(ergebnis.Perioden);
        Assert.Contains("2020", periode.Beschriftung);
        Assert.Contains("53", periode.Beschriftung);
        Assert.Equal(2, periode.Anzahl);
    }

    [Fact]
    public async Task UngueltigesRasterUndUmgekehrterZeitraumWerdenAbgewiesen()
    {
        var von = new DateOnly(2026, 2, 1);
        var bis = new DateOnly(2026, 1, 1);

        await Assert.ThrowsAsync<UngueltigeTourException>(() =>
            _auswertung.Laden(new AktivitaetsauswertungAnfrage(von, von, (Auswertungsraster)999, null, null)));
        await Assert.ThrowsAsync<UngueltigeTourException>(() =>
            _auswertung.Laden(new AktivitaetsauswertungAnfrage(von, bis, Auswertungsraster.Tag, null, null)));
    }

    private async Task LegeTourAn(DateOnly datum)
    {
        var werte = TourenMapper.ToAntwort(new Trainingseinheit
        {
            AktivitaetId = _aktivitaetId,
            Datum = datum,
            Name = "Test",
            DistanzKm = 10,
            Fahrzeit = TimeSpan.FromMinutes(30)
        }).Werte;

        await _touren.Anlegen(werte);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_ordner))
            Directory.Delete(_ordner, recursive: true);
    }
}
