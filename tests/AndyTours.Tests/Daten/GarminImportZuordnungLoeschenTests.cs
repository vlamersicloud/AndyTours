using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndyTours.Tests.Daten;

public sealed class GarminImportZuordnungLoeschenTests
{
    [Fact]
    public void LoeschenEntferntNurBetroffeneMappingsUndVerhindertIdReuseLink()
    {
        var path = Path.Combine(Path.GetTempPath(), "GarminMappingDelete-" + Guid.NewGuid().ToString("N") + ".db");
        var archive = Path.Combine(Path.GetTempPath(), "garmin-archive-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (var db = OeffneDatenbank.Ausfuehren(path))
            using (var tx = db.BeginTransaction())
            {
                AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Rad", IstHaupt = true });
                var first = TrainingseinheitenTabelle.Einfuegen(db, tx,
                    new Trainingseinheit { Datum = new DateOnly(2026, 5, 1), Name = "mapped", AktivitaetId = 1 },
                    (connection, transaction, id) => GarminImportZuordnung.Speichern(connection, transaction, 987654, id));
                EinstellungTabelle.Schreiben(db, tx, "Unrelated.Preference", "keep");
                File.WriteAllBytes(archive, [1, 2, 3]);
                tx.Commit();
                Assert.Equal(1, first.Id);
            }

            using (var db = OeffneDatenordner(path)) TrainingseinheitenTabelle.Loeschen(db, [1]);

            using (var db = OeffneDatenordner(path))
            using (var tx = db.BeginTransaction())
            {
                var unrelated = TrainingseinheitenTabelle.Einfuegen(db, tx,
                    new Trainingseinheit { Datum = new DateOnly(2026, 5, 2), Name = "unrelated", AktivitaetId = 1 });
                tx.Commit();
                Assert.Equal(1, unrelated.Id);
            }
            using (var verify = OeffneDatenordner(path))
            {
                Assert.Null(EinstellungTabelle.Lesen(verify, "GarminConnect.Aktivitaet.987654"));
                Assert.Equal("keep", EinstellungTabelle.Lesen(verify, "Unrelated.Preference"));
                Assert.True(File.Exists(archive));
                Assert.DoesNotContain(987654L, GarminImportZuordnung.Laden(verify, new HashSet<long> { 1 }));
            }
        }
        finally
        {
            try { File.Delete(path); } catch (IOException) { }
            try { File.Delete(archive); } catch (IOException) { }
        }
    }

    [Fact]
    public void RollbackStelltGelöschtesMappingWiederHer()
    {
        var path = Path.Combine(Path.GetTempPath(), "GarminMappingRollback-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            long id;
            using (var db = OeffneDatenordner(path))
            using (var tx = db.BeginTransaction())
            {
                AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Rad", IstHaupt = true });
                id = TrainingseinheitenTabelle.Einfuegen(db, tx,
                    new Trainingseinheit { Datum = new DateOnly(2026, 5, 1), Name = "mapped", AktivitaetId = 1 },
                    (connection, transaction, tourId) => GarminImportZuordnung.Speichern(connection, transaction, 123456, tourId)).Id;
                tx.Commit();
            }
            using (var db = OeffneDatenordner(path))
            using (var tx = db.BeginTransaction())
            {
                GarminImportZuordnung.EntferneFuerEinheiten(db, tx, new HashSet<long> { id });
                tx.Rollback();
            }
            using var verify = OeffneDatenordner(path);
            Assert.Equal(id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                EinstellungTabelle.Lesen(verify, "GarminConnect.Aktivitaet.123456"));
        }
        finally { try { File.Delete(path); } catch (IOException) { } }
    }

    private static SqliteConnection OeffneDatenordner(string path) => OeffneDatenbank.Ausfuehren(path);
}
