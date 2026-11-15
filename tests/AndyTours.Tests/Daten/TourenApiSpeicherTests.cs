using Microsoft.Data.Sqlite;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Xunit;

namespace AndyTours.Tests.Daten;

/// <summary>Regressionstests für den SQLite-Tourenspeicher mit isolierter temporärer Datenbank.</summary>
public sealed class TourenApiSpeicherTests : IDisposable
{
    private readonly string _ordner = Path.Combine(Path.GetTempPath(), "AndyTours-Touren-" + Guid.NewGuid().ToString("N"));
    private readonly string _datenbank;
    private readonly SqliteTourenSpeicher _speicher;
    private readonly long _aktivitaetId;

    public TourenApiSpeicherTests()
    {
        Directory.CreateDirectory(_ordner);
        _datenbank = Path.Combine(_ordner, "andytours.db");
        _speicher = new SqliteTourenSpeicher(_datenbank);
        using var verbindung = OeffneDatenbank.Ausfuehren(_datenbank);
        using var transaktion = verbindung.BeginTransaction();
        _aktivitaetId = AktivitaetenTabelle.Einfuegen(verbindung, transaktion,
            new Aktivitaet { Name = "Rad" });
        transaktion.Commit();
    }

    [Fact]
    public async Task Anlegen_ErzeugtStandardfahrerUndLesbareVersion()
    {
        var angelegt = await _speicher.Anlegen(Werte());
        var gelesen = await _speicher.Lesen(angelegt.Id);

        Assert.True(angelegt.Werte.FahrerId > 0);
        Assert.Equal(angelegt.Version, gelesen!.Version);
    }

    [Fact]
    public async Task Aendern_MitGelesenerVersion_AktualisiertWerte()
    {
        var erstellt = await _speicher.Anlegen(Werte());
        var aenderung = await _speicher.Aendern(erstellt.Id,
            new TourAendernAnfrage(erstellt.Version, erstellt.Werte with { Name = "Neu" }));

        Assert.Equal("Neu", aenderung.Werte.Name);
        Assert.NotEqual(erstellt.Version, aenderung.Version);
    }

    [Fact]
    public async Task VeralteteAenderungUndLoeschungWerdenAbgewiesen()
    {
        var erstellt = await _speicher.Anlegen(Werte());
        var aktuell = await _speicher.Aendern(erstellt.Id,
            new TourAendernAnfrage(erstellt.Version, erstellt.Werte with { Name = "Neu" }));

        await Assert.ThrowsAsync<TourVersionskonfliktException>(() =>
            _speicher.Aendern(erstellt.Id, new TourAendernAnfrage(erstellt.Version, erstellt.Werte with { Name = "Veraltet" })));
        await Assert.ThrowsAsync<TourVersionskonfliktException>(() =>
            _speicher.Loeschen(erstellt.Id, erstellt.Version));

        Assert.Equal("Neu", (await _speicher.Lesen(erstellt.Id))!.Werte.Name);
        Assert.Equal(aktuell.Version, (await _speicher.Lesen(erstellt.Id))!.Version);
    }

    [Fact]
    public async Task Loeschen_MitFrischerVersion_EntferntTour()
    {
        var erstellt = await _speicher.Anlegen(Werte());
        await _speicher.Loeschen(erstellt.Id, erstellt.Version);
        Assert.Null(await _speicher.Lesen(erstellt.Id));
    }

    [Fact]
    public async Task Liste_FiltertZeitraumUndSeitengroesse()
    {
        await _speicher.Anlegen(Werte() with { Datum = new DateOnly(2026, 1, 1), Name = "A" });
        await _speicher.Anlegen(Werte() with { Datum = new DateOnly(2026, 2, 1), Name = "B" });
        var seite = await _speicher.Liste(0, 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1));
        Assert.Equal(2, seite.Gesamt);
        Assert.Single(seite.Eintraege);
        Assert.Equal("A", seite.Eintraege[0].Werte.Name);
    }

    [Fact]
    public async Task UngueltigePulswerteUndSekundenzeitWerdenAbgewiesen()
    {
        await Assert.ThrowsAsync<UngueltigeTourException>(() =>
            _speicher.Anlegen(Werte() with { HfMax = -1 }));
        await Assert.ThrowsAsync<UngueltigeTourException>(() =>
            _speicher.Anlegen(Werte() with { Startzeit = new TimeOnly(8, 15, 1) }));
    }

    [Fact]
    public async Task Fahrzeit_MaximalSichereSekundenzahl_BleibtBeimLesenErhalten()
    {
        var sekunden = TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerSecond;
        var angelegt = await _speicher.Anlegen(Werte() with { FahrzeitSekunden = sekunden });
        var gelesen = await _speicher.Lesen(angelegt.Id);
        Assert.Equal(sekunden, gelesen!.Werte.FahrzeitSekunden);
    }

    private TourWerte Werte() =>
        TourenMapper.ToAntwort(new Trainingseinheit
        {
            AktivitaetId = _aktivitaetId,
            Datum = new DateOnly(2026, 1, 1),
            Name = "Test",
            DistanzKm = 10,
            Fahrzeit = TimeSpan.FromMinutes(30)
        }).Werte;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_ordner))
            Directory.Delete(_ordner, recursive: true);
    }
}
