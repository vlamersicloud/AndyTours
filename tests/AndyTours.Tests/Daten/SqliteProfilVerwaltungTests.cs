using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

/// <summary>Isolierte Profilverwaltungstests mit temporärer SQLite-Datenbank.</summary>
public sealed class SqliteProfilVerwaltungTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AndyTours-ProfilApi-" + Guid.NewGuid().ToString("N"));
    private readonly string _db;
    private readonly SqliteProfilVerwaltung _sut;
    public SqliteProfilVerwaltungTests() { Directory.CreateDirectory(_dir); _db = Path.Combine(_dir, "andytours.db"); _sut = new(_db); }

    [Fact] public async Task LadenLeererBestandErzeugtKeinenFahrer()
    {
        var response = await _sut.Laden(); Assert.Null(response.Fahrer);
        using var c = OeffneDatenbank.Ausfuehren(_db); Assert.Empty(FahrerTabelle.Laden(c));
    }

    [Fact] public async Task ErstesSpeichernTrimmtUndRundtripptAlleFelderUndID()
    {
        var values = new ProfilWerte(" Ada ", " Lovelace ", new DateOnly(2020, 2, 29), 170, 62.5, 60, 180, " London ");
        var first = await _sut.Speichern(new((await _sut.Laden()).Version, values));
        Assert.NotNull(first.Fahrer); Assert.Equal("Ada", first.Fahrer.Vorname); Assert.Equal("Lovelace", first.Fahrer.Nachname);
        Assert.Equal(new DateOnly(2020, 2, 29), first.Fahrer.Geburtstag); Assert.Equal(170, first.Fahrer.GroesseCm);
        Assert.Equal(62.5, first.Fahrer.GewichtKg); Assert.Equal(60, first.Fahrer.Ruhepuls); Assert.Equal(180, first.Fahrer.Maxpuls); Assert.Equal("London", first.Fahrer.Heimatort);
        var second = await _sut.Speichern(new(first.Version, values with { Vorname = "Grace" }));
        Assert.Equal(first.Fahrer.Id, second.Fahrer!.Id); Assert.Equal("Grace", second.Fahrer.Vorname);
    }

    [Fact] public async Task VeralteteVersionLaesstProfilUnveraendert()
    {
        var old = await _sut.Laden(); var saved = await _sut.Speichern(new(old.Version, Values("A")));
        var ex = await Assert.ThrowsAsync<StammdatenException>(() => _sut.Speichern(new(old.Version, Values("B"))));
        Assert.Equal(StammdatenFehlerart.Konflikt, ex.Art); Assert.Equal("A", (await _sut.Laden()).Fahrer!.Vorname);
    }

    [Fact] public async Task BeiMehrerenLegacyFahrernWirdNurDerErsteGeaendert()
    {
        using (var c = OeffneDatenbank.Ausfuehren(_db)) using (var tx = c.BeginTransaction())
        {
            FahrerTabelle.Speichern(c, tx, [new Fahrer { Id = 10, Vorname = "Erster" }, new Fahrer { Id = 20, Vorname = "Zweiter" }]); tx.Commit();
        }
        var response = await _sut.Speichern(new((await _sut.Laden()).Version, Values("Neu")));
        Assert.Equal(10, response.Fahrer!.Id);
        using var db = OeffneDatenbank.Ausfuehren(_db); var all = FahrerTabelle.Laden(db);
        Assert.Equal("Neu", all.Single(f => f.Id == 10).Vorname); Assert.Equal("Zweiter", all.Single(f => f.Id == 20).Vorname);
    }

    [Fact] public async Task UngueltigeWerteWerdenAbgelehntOhneVersionsaenderung()
    {
        var baseSnapshot = await _sut.Speichern(new((await _sut.Laden()).Version, Values("Valid")));
        var invalid = new[] { Values("x") with { Geburtstag = DateOnly.FromDateTime(DateTime.Today).AddDays(1) },
            Values("x") with { GroesseCm = -1 }, Values("x") with { GewichtKg = double.NaN }, Values("x") with { GewichtKg = double.PositiveInfinity },
            Values("x") with { GewichtKg = 60.25 }, Values("x") with { Ruhepuls = 80, Maxpuls = 70 } };
        foreach (var value in invalid)
        {
            var ex = await Assert.ThrowsAsync<StammdatenException>(() => _sut.Speichern(new(baseSnapshot.Version, value)));
            Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art); Assert.Equal(baseSnapshot.Version, (await _sut.Laden()).Version);
        }
    }

    [Fact] public async Task LeereTexteUndUnbekannteZahlenwerteSindGueltig()
    {
        var saved = await _sut.Speichern(new((await _sut.Laden()).Version, new ProfilWerte("", "", null, 0, 0, 0, 0, "")));
        Assert.NotNull(saved.Fahrer); Assert.Equal(string.Empty, saved.Fahrer.Vorname); Assert.Equal(0, saved.Fahrer.GroesseCm);
        Assert.Equal(0, saved.Fahrer.GewichtKg); Assert.Null(saved.Fahrer.Geburtstag);
    }

    [Fact] public async Task TransaktionsOverloadRollbacktMitAufrufer()
    {
        var before = await _sut.Laden();
        using (var c = OeffneDatenbank.Ausfuehren(_db))
        using (var tx = c.BeginTransaction())
        {
            FahrerTabelle.SpeicherePersoenlicheDaten(c, tx, new Fahrer { Vorname = "Uncommitted" });
        }
        Assert.Equal(before.Version, (await _sut.Laden()).Version); Assert.Null((await _sut.Laden()).Fahrer);
    }

    private static ProfilWerte Values(string name) => new(name, "Nachname", null, 0, 0, 0, 0, "");
    public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
}
