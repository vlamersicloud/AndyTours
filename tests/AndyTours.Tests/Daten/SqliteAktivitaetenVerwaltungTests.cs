using AndyTours.Application.Stammdaten;
using AndyTours.Core.Modell;
using AndyTours.Daten;

namespace AndyTours.Tests.Daten;

/// <summary>Regressionen für versionierte Aktivitätsverwaltung auf separaten SQLite-Dateien.</summary>
public sealed class SqliteAktivitaetenVerwaltungTests : IDisposable
{
    private readonly string _ordner = Path.Combine(Path.GetTempPath(), "AndyTours-AktivitaetenApi-" + Guid.NewGuid().ToString("N"));
    private readonly string _datenbank;
    private readonly SqliteAktivitaetenVerwaltung _verwaltung;

    public SqliteAktivitaetenVerwaltungTests()
    {
        Directory.CreateDirectory(_ordner);
        _datenbank = Path.Combine(_ordner, "andytours.db");
        _verwaltung = new SqliteAktivitaetenVerwaltung(_datenbank);
    }

    [Fact]
    public async Task ErsteAktivitaetWirdAutomatischHauptaktivitaet()
    {
        var leer = await _verwaltung.Laden();
        var ergebnis = await _verwaltung.Anlegen(new(leer.Version, "Rad"));
        var aktivitaet = Assert.Single(ergebnis.Eintraege);
        Assert.True(aktivitaet.IstHaupt);
    }

    [Fact]
    public async Task VeralteteFamilienversionBlockiertSchreibzugriff()
    {
        var erste = await _verwaltung.Anlegen(new((await _verwaltung.Laden()).Version, "Rad"));
        await _verwaltung.Anlegen(new(erste.Version, "Laufen"));
        var fehler = await Assert.ThrowsAsync<StammdatenException>(() =>
            _verwaltung.Anlegen(new(erste.Version, "Schwimmen")));
        Assert.Equal(StammdatenFehlerart.Konflikt, fehler.Art);
        Assert.Equal(2, (await _verwaltung.Laden()).Eintraege.Count);
    }

    [Fact]
    public async Task ReihenfolgeBenötigtExaktePermutationUndAktualisiertVersion()
    {
        var snapshot = await _verwaltung.Anlegen(new((await _verwaltung.Laden()).Version, "Rad"));
        snapshot = await _verwaltung.Anlegen(new(snapshot.Version, "Laufen"));
        var ids = snapshot.Eintraege.Select(a => a.Id).Reverse().ToArray();
        var geordnet = await _verwaltung.SetzeReihenfolge(new(snapshot.Version, ids));
        Assert.Equal(ids, geordnet.Eintraege.Select(a => a.Id));
        var fehler = await Assert.ThrowsAsync<StammdatenException>(() =>
            _verwaltung.SetzeReihenfolge(new(geordnet.Version, [ids[0], ids[0]])));
        Assert.Equal(StammdatenFehlerart.Ungueltig, fehler.Art);
    }

    [Fact]
    public async Task UmbenennenBewahrtEinheitUndGrundziele()
    {
        using (var c = OeffneDatenbank.Ausfuehren(_datenbank))
        using (var tx = c.BeginTransaction())
        {
            AktivitaetenTabelle.Einfuegen(c, tx, new Aktivitaet
            {
                Name = "Rad", IstHaupt = true, Geschwindigkeitseinheit = "min/km",
                ZielKm = 123, ZielHm = 456, ZielAnzahl = 7
            });
            tx.Commit();
        }
        var snapshot = await _verwaltung.Laden();
        var renamed = await _verwaltung.Umbenennen(snapshot.Eintraege[0].Id, new(snapshot.Version, "Laufen"));
        var item = Assert.Single(renamed.Eintraege);
        Assert.Equal("Laufen", item.Name);
        Assert.True(item.IstHaupt);
        Assert.Equal("min/km", item.Geschwindigkeitseinheit);
        Assert.Equal(123, item.ZielKm);
        Assert.Equal(456, item.ZielHm);
        Assert.Equal(7, item.ZielAnzahl);
    }

    [Fact]
    public async Task HauptUndVerwendeteAktivitaetSindNichtLoeschbar()
    {
        var snapshot = await _verwaltung.Anlegen(new((await _verwaltung.Laden()).Version, "Rad"));
        snapshot = await _verwaltung.Anlegen(new(snapshot.Version, "Laufen"));
        var main = snapshot.Eintraege.Single(a => a.IstHaupt);
        var secondary = snapshot.Eintraege.Single(a => !a.IstHaupt);
        var mainError = await Assert.ThrowsAsync<StammdatenException>(() => _verwaltung.Loeschen(main.Id, snapshot.Version));
        Assert.Equal(StammdatenFehlerart.Konflikt, mainError.Art);
        using (var c = OeffneDatenbank.Ausfuehren(_datenbank))
        using (var tx = c.BeginTransaction())
        {
            EquipmentsTabelle.Einfuegen(c, tx, new Equipment { Name = "Bike", AktivitaetId = secondary.Id });
            tx.Commit();
        }
        var current = await _verwaltung.Laden();
        var usedError = await Assert.ThrowsAsync<StammdatenException>(() => _verwaltung.Loeschen(secondary.Id, current.Version));
        Assert.Equal(StammdatenFehlerart.Konflikt, usedError.Art);
    }

    [Fact]
    public async Task ZusammenfuehrenÜberträgtEquipmentUndEntferntQuelleAusReihenfolge()
    {
        var snapshot = await _verwaltung.Anlegen(new((await _verwaltung.Laden()).Version, "Rad"));
        snapshot = await _verwaltung.Anlegen(new(snapshot.Version, "Laufen"));
        var source = snapshot.Eintraege.Single(a => !a.IstHaupt);
        var target = snapshot.Eintraege.Single(a => a.IstHaupt);
        using (var c = OeffneDatenbank.Ausfuehren(_datenbank))
        using (var tx = c.BeginTransaction())
        {
            EquipmentsTabelle.Einfuegen(c, tx, new Equipment { Name = "Device", AktivitaetId = source.Id });
            tx.Commit();
        }
        var merged = await _verwaltung.Zusammenfuehren(new(snapshot.Version, source.Id, target.Id));
        Assert.DoesNotContain(merged.Eintraege, a => a.Id == source.Id);
        Assert.Single(merged.Eintraege);
        using var read = OeffneDatenbank.Ausfuehren(_datenbank);
        Assert.Equal(target.Id, Assert.Single(EquipmentsTabelle.Laden(read)).AktivitaetId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_ordner)) Directory.Delete(_ordner, recursive: true);
    }
}
