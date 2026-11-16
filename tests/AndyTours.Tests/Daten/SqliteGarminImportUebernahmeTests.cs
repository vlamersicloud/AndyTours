using System.Globalization;
using System.Text.Json;
using AndyTours.Application.Garmin;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndyTours.Tests.Daten;

public sealed class SqliteGarminImportUebernahmeTests
{
    [Fact]
    public async Task ImportiertAtomarUndReplayIstIdempotent()
    {
        var root = Path.Combine(Path.GetTempPath(), "GarminUebernahme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var dbPath = Path.Combine(root, "db.sqlite");
            CreateDatabase(dbPath);
            var jobs = new SqliteGarminImportJobs(dbPath);
            await jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid()));
            var lease = (await jobs.Beanspruchen(Guid.NewGuid(), TimeSpan.FromMinutes(5)))!;
            var kandidatId = Guid.NewGuid();
            await jobs.DownloadKandidatSpeichern(lease, new GarminImportKandidatAntwort(kandidatId, 901, GarminImportKandidatStatus.FitBereit, new DateOnly(2026, 4, 1), "fixture", 20, 3600, null));
            var ready = await jobs.PaketAbschliessen(lease, false, false, []);
            var aufzeichnung = new Aufzeichnung { Datum = new DateOnly(2026, 4, 1), Startzeit = new TimeOnly(10, 30), Fahrzeit = TimeSpan.FromHours(1), DistanzKm = 20 };
            var anfrage = new GarminImportUebernahmeAnfrage(ready.Version, Guid.NewGuid(), [new GarminImportAuswahl(kandidatId, new string('A', 64), true, null)]);
            var vorbereitet = new GarminImportVorbereitung(kandidatId, aufzeichnung, $"2026/garmin-901-{kandidatId:N}.fit");
            var result = await jobs.Uebernehmen(ready.JobId, anfrage, [vorbereitet], CancellationToken.None);
            Assert.Equal(GarminImportJobStatus.Fertig, result.Auftrag.Status);
            Assert.Equal(1, result.Auftrag.Importiert);
            Assert.Equal(0, result.Auftrag.Bereit);
            var outcome = Assert.Single(result.Ergebnisse);
            Assert.True(outcome.Importiert);
            var tourId = Assert.IsType<long>(outcome.TourId);
            Assert.True(tourId > 0);
            using (var db = OeffneDatenbank.Ausfuehren(dbPath))
            {
                var stored = Assert.Single(TrainingseinheitenTabelle.Laden(db));
                Assert.Single(FahrerTabelle.Laden(db));
                Assert.Equal(2, stored.Anhaenge.Count);
                Assert.Contains(901L, GarminImportZuordnung.Laden(db, new HashSet<long> { stored.Id }));
                Assert.Equal(tourId, long.Parse(EinstellungTabelle.Lesen(db, "GarminConnect.Aktivitaet.901")!, CultureInfo.InvariantCulture));
            }
            var replay = await jobs.Uebernehmen(ready.JobId, anfrage, [], CancellationToken.None);
            Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(replay));
            using var verify = OeffneDatenbank.Ausfuehren(dbPath);
            Assert.Single(TrainingseinheitenTabelle.Laden(verify));
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    [Fact]
    public async Task BatchInsertFailureRollsBackEveryDatabaseWrite()
    {
        var root = Path.Combine(Path.GetTempPath(), "GarminRollback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var dbPath = Path.Combine(root, "db.sqlite");
            CreateDatabase(dbPath);
            var jobs = new SqliteGarminImportJobs(dbPath);
            await jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid()));
            var lease = (await jobs.Beanspruchen(Guid.NewGuid(), TimeSpan.FromMinutes(5)))!;
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            await jobs.DownloadKandidatSpeichern(lease, new GarminImportKandidatAntwort(first, 902, GarminImportKandidatStatus.FitBereit, new DateOnly(2026, 4, 1), "first", 20, 3600, null));
            await jobs.DownloadKandidatSpeichern(lease, new GarminImportKandidatAntwort(second, 903, GarminImportKandidatStatus.FitBereit, new DateOnly(2026, 4, 2), "second", 20, 3600, null));
            var ready = await jobs.PaketAbschliessen(lease, false, false, []);
            using (var db = OeffneDatenbank.Ausfuehren(dbPath))
            using (var command = db.CreateCommand())
            {
                command.CommandText = "CREATE TRIGGER FailSecond BEFORE INSERT ON Trainingseinheit WHEN NEW.Datum='2026-04-02' BEGIN SELECT RAISE(ABORT,'fixture_failure'); END;";
                command.ExecuteNonQuery();
            }
            var request = new GarminImportUebernahmeAnfrage(ready.Version, Guid.NewGuid(), [
                new GarminImportAuswahl(first, new string('A', 64), true, null),
                new GarminImportAuswahl(second, new string('B', 64), true, null)]);
            var prepared = new[] {
                new GarminImportVorbereitung(first, new Aufzeichnung { Datum = new DateOnly(2026, 4, 1), Startzeit = new TimeOnly(10, 0), Fahrzeit = TimeSpan.FromHours(1), DistanzKm = 20 }, $"2026/garmin-902-{first:N}.fit"),
                new GarminImportVorbereitung(second, new Aufzeichnung { Datum = new DateOnly(2026, 4, 2), Startzeit = new TimeOnly(10, 0), Fahrzeit = TimeSpan.FromHours(1), DistanzKm = 20 }, $"2026/garmin-903-{second:N}.fit") };
            await Assert.ThrowsAsync<SqliteException>(() => jobs.Uebernehmen(ready.JobId, request, prepared, CancellationToken.None));
            using var verify = OeffneDatenbank.Ausfuehren(dbPath);
            Assert.Empty(TrainingseinheitenTabelle.Laden(verify));
            Assert.Empty(FahrerTabelle.Laden(verify));
            Assert.Equal(0, Scalar(verify, "SELECT COUNT(*) FROM Anhang;"));
            Assert.Equal(0, Scalar(verify, "SELECT COUNT(*) FROM Einstellung WHERE Schluessel LIKE 'GarminConnect.Aktivitaet.%';"));
            Assert.Equal(0, Scalar(verify, "SELECT COUNT(*) FROM GarminImportSteuerung;"));
            Assert.Equal(2, (await jobs.Kandidaten(ready.JobId, 0, 20)).Kandidaten.Count(c => c.Status == GarminImportKandidatStatus.FitBereit));
            Assert.Equal(ready.Version, (await jobs.Laden(ready.JobId))!.Version);
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    private static void CreateDatabase(string path)
    {
        using var db = OeffneDatenbank.Ausfuehren(path);
        using var tx = db.BeginTransaction();
        AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Rad", IstHaupt = true });
        EinstellungTabelle.Schreiben(db, tx, Einstellungsschluessel.GarminPersonalApiKey, "FAKE-ONLY-TEST-KEY");
        tx.Commit();
    }

    private static long Scalar(SqliteConnection db, string sql)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}
