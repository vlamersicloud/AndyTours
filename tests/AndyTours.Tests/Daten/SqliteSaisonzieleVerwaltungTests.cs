using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

/// <summary>Isolierte CAS-, Jahresbereichs- und Rollbacktests für Saisonziele.</summary>
public sealed class SqliteSaisonzieleVerwaltungTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AndyTours-SaisonApi-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;
    private readonly SqliteSaisonzieleVerwaltung _service;
    private readonly long _a1, _a2;
    public SqliteSaisonzieleVerwaltungTests()
    {
        Directory.CreateDirectory(_dir); _path = Path.Combine(_dir, "andytours.db");
        _service = new SqliteSaisonzieleVerwaltung(_path);
        using var db = OeffneDatenbank.Ausfuehren(_path); using var tx = db.BeginTransaction();
        _a1 = AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Rad", ZielKm = 1000, ZielHm = 500, ZielAnzahl = 20 });
        _a2 = AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Laufen", ZielKm = 300, ZielHm = 100, ZielAnzahl = 30 });
        tx.Commit();
    }

    [Fact]
    public async Task LeeresLadenErzeugtKeineSaisonziele()
    {
        var result = await _service.Laden(); Assert.Empty(result.Ziele);
        using var db = OeffneDatenbank.Ausfuehren(_path);
        using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM Saisonziel;";
        Assert.Equal(0L, cmd.ExecuteScalar());
    }

    [Fact]
    public async Task ErsetztNurAusgewaehlteJahreUndBewahrtBasisziele()
    {
        var loaded = await _service.Laden();
        var first = await _service.Speichern(new(loaded.Version, [2025, 2026], [new(_a1, 2025, 1200, 600, 22), new(_a1, 2026, 1300, 700, 24)]));
        var second = await _service.Speichern(new(first.Version, [2026], [new(_a2, 2026, 0, 0, 0)]));
        Assert.Contains(second.Ziele, x => x.AktivitaetId == _a1 && x.AbJahr == 2025 && x.ZielKm == 1200);
        Assert.DoesNotContain(second.Ziele, x => x.AktivitaetId == _a1 && x.AbJahr == 2026);
        Assert.Contains(second.Ziele, x => x.AktivitaetId == _a2 && x.AbJahr == 2026 && x.ZielKm == 0 && x.ZielHm == 0 && x.ZielAnzahl == 0);
        var baseGoals = second.Aktivitaeten.Single(x => x.Id == _a1);
        Assert.Equal(1000, baseGoals.ZielKm); Assert.Equal(500, baseGoals.ZielHm); Assert.Equal(20, baseGoals.ZielAnzahl);
        using var db = OeffneDatenbank.Ausfuehren(_path);
        Assert.Contains(SaisonzieleTabelle.Laden(db), x => x.AktivitaetId == _a1 && x.AbJahr == 2025 && x.ZielKm == 1200);
    }

    [Fact]
    public async Task LeereZieleEntfernenNurOverridesDerGewaehltenJahre()
    {
        var loaded = await _service.Laden();
        var saved = await _service.Speichern(new(loaded.Version, [2025, 2026], [new(_a1, 2025, 10, 20, 30), new(_a2, 2026, 40, 50, 60)]));
        var cleared = await _service.Speichern(new(saved.Version, [2025], []));
        Assert.DoesNotContain(cleared.Ziele, x => x.AbJahr == 2025);
        Assert.Contains(cleared.Ziele, x => x.AbJahr == 2026 && x.AktivitaetId == _a2);
        using var db = OeffneDatenbank.Ausfuehren(_path); Assert.Single(SaisonzieleTabelle.Laden(db));
    }

    [Fact]
    public async Task VeralteteVersionNachZielaenderungErhaeltDatenbank()
    {
        var stale = await _service.Laden();
        var saved = await _service.Speichern(new(stale.Version, [2025], [new(_a1, 2025, 1, 2, 3)]));
        var error = await Assert.ThrowsAsync<StammdatenException>(() => _service.Speichern(new(stale.Version, [2025], [])));
        Assert.Equal(StammdatenFehlerart.Konflikt, error.Art);
        using var db = OeffneDatenbank.Ausfuehren(_path);
        Assert.Contains(SaisonzieleTabelle.Laden(db), x => x.AbJahr == 2025 && x.ZielKm == 1);
        Assert.NotEqual(stale.Version, saved.Version);
    }

    [Fact]
    public async Task VeralteteVersionNachBasiszielaenderungWirdAbgelehnt()
    {
        var stale = await _service.Laden();
        using (var db = OeffneDatenbank.Ausfuehren(_path))
        using (var tx = db.BeginTransaction())
        {
            var activity = AktivitaetenTabelle.Laden(db).Single(x => x.Id == _a1); activity.ZielKm = 1700;
            AktivitaetenTabelle.Aendern(db, tx, activity); tx.Commit();
        }
        var error = await Assert.ThrowsAsync<StammdatenException>(() => _service.Speichern(new(stale.Version, [2025], [])));
        Assert.Equal(StammdatenFehlerart.Konflikt, error.Art);
        using var verify = OeffneDatenbank.Ausfuehren(_path); Assert.Empty(SaisonzieleTabelle.Laden(verify));
    }

    [Fact]
    public async Task UngueltigeAnfragenWerdenVorJahresloeschungAbgewiesen()
    {
        var loaded = await _service.Laden();
        var existing = await _service.Speichern(new(loaded.Version, [2025], [new(_a1, 2025, 8, 9, 10)]));
        var invalid = new[]
        {
            new SaisonzieleSpeichernAnfrage(existing.Version, [2025], [new(long.MaxValue, 2025, 1, 1, 1)]),
            new SaisonzieleSpeichernAnfrage(existing.Version, [2025], [new(_a1, 2025, -1, 0, 0)]),
            new SaisonzieleSpeichernAnfrage(existing.Version, [2025], [new(_a1, 2025, 1, 1, 1), new(_a1, 2025, 2, 2, 2)]),
            new SaisonzieleSpeichernAnfrage(existing.Version, [2025], [new(_a1, 2026, 1, 1, 1)]),
            new SaisonzieleSpeichernAnfrage(existing.Version, [2025, 2025], [])
        };
        foreach (var request in invalid)
        {
            var error = await Assert.ThrowsAsync<StammdatenException>(() => _service.Speichern(request));
            Assert.Equal(request.Ziele.Any(g => g.AktivitaetId == long.MaxValue) ? StammdatenFehlerart.NichtGefunden : StammdatenFehlerart.Ungueltig, error.Art);
            using var db = OeffneDatenbank.Ausfuehren(_path);
            Assert.Contains(SaisonzieleTabelle.Laden(db), x => x.AbJahr == 2025 && x.ZielKm == 8);
        }
    }

    [Fact]
    public void CallerTransactionRollbackRestoresSeasonalRows()
    {
        using var db = OeffneDatenbank.Ausfuehren(_path);
        using (var seed = db.BeginTransaction())
        {
            SaisonzieleTabelle.Speichern(db, seed, [new Saisonziel { AktivitaetId = _a1, AbJahr = 2025, ZielKm = 77 }]); seed.Commit();
        }
        using (var outer = db.BeginTransaction())
            SaisonzieleTabelle.SpeichereJahre(db, outer, [2025], [new Saisonziel { AktivitaetId = _a2, AbJahr = 2025, ZielKm = 88 }]);
        var restored = SaisonzieleTabelle.Laden(db);
        var row = Assert.Single(restored); Assert.Equal(_a1, row.AktivitaetId); Assert.Equal(77, row.ZielKm);
    }

    public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
}
