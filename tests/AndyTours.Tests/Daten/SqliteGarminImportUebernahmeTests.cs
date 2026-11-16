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
            using (var db = OeffneDatenbank.Ausfuehren(dbPath))
            using (var tx = db.BeginTransaction())
            {
                AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Rad", IstHaupt = true });
                EinstellungTabelle.Schreiben(db, tx, Einstellungsschluessel.GarminPersonalApiKey, "FAKE-ONLY-TEST-KEY");
                tx.Commit();
            }
            var jobs = new SqliteGarminImportJobs(dbPath);
            await jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid()));
            var lease = (await jobs.Beanspruchen(Guid.NewGuid(), TimeSpan.FromMinutes(5)))!;
            var kandidatId = Guid.NewGuid();
            await jobs.DownloadKandidatSpeichern(lease, new GarminImportKandidatAntwort(kandidatId, 901, GarminImportKandidatStatus.FitBereit,
                new DateOnly(2026, 4, 1), "fixture", 20, 3600, null));
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
}
