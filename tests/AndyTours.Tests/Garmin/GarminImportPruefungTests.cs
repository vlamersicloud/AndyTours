using AndyTours.Application.Garmin;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Import.Garmin;
using AndyTours.Tests.Datenordner;
using Xunit;

namespace AndyTours.Tests.Garmin;

[Collection(FitTestdatei.Testgruppe)]
public sealed class GarminImportPruefungTests
{
    [Fact]
    public async Task ReviewUsesFitSnapshotAndReceiptPrecedesSourceRead()
    {
        using var f = new Fixture();
        var (job, candidate, review, facade) = await f.Prepare(new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc));
        var row = Assert.Single(review.Zeilen);
        Assert.Equal(90.8307, row.DistanzKm, 3);
        Assert.Equal(new DateOnly(2026, 4, 1), row.Datum);
        Assert.Equal(42L, row.EmpfohlenesEquipmentId);
        var request = new GarminImportUebernahmeAnfrage(job.Version, Guid.NewGuid(), [new GarminImportAuswahl(candidate.KandidatId, row.DateiVersion, true, 42)]);
        var response = await facade.Uebernehmen(job.JobId, request);
        var tour = Assert.Single(response.Ergebnisse);
        Assert.True(tour.Importiert);
        var stored = Assert.Single(f.ReadTours());
        Assert.Equal(2, stored.Anhaenge.Count);
        Assert.Contains(stored.Anhaenge, a => a.RelativerPfad == $"2026/garmin-{candidate.GarminAktivitaetId}-{candidate.KandidatId:N}.fit");
        Assert.True(File.Exists(f.FitPath(job.JobId, candidate.KandidatId)));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(f.FitPath(job.JobId, candidate.KandidatId))!, ".review-*.tmp"));
        File.Delete(f.FitPath(job.JobId, candidate.KandidatId));
        var replay = await facade.Uebernehmen(job.JobId, request);
        Assert.Equal(tour.TourId, Assert.Single(replay.Ergebnisse).TourId);
        Assert.Single(f.ReadTours());
    }

    [Fact]
    public async Task ChangedFitVersionRejectsImportWithoutTour()
    {
        using var f = new Fixture();
        var (job, candidate, review, facade) = await f.Prepare(new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc));
        var row = Assert.Single(review.Zeilen);
        var request = new GarminImportUebernahmeAnfrage(job.Version, Guid.NewGuid(), [new GarminImportAuswahl(candidate.KandidatId, row.DateiVersion, true, null)]);
        FitTestdatei.Schreibe(f.FitPath(job.JobId, candidate.KandidatId), new FitTestdatei.Werte(new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)));
        await Assert.ThrowsAsync<AndyTours.Application.Stammdaten.StammdatenException>(() => facade.Uebernehmen(job.JobId, request));
        Assert.Empty(f.ReadTours());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "GarminReview-" + Guid.NewGuid().ToString("N"));
        private string DbPath => Path.Combine(_root, "db.sqlite");
        public Fixture() { Directory.CreateDirectory(_root); }
        public string FitPath(Guid jobId, Guid candidateId) => Path.Combine(_root, "GarminImport", jobId.ToString("N"), candidateId.ToString("N") + ".fit");
        public List<Trainingseinheit> ReadTours() { using var db = OeffneDatenbank.Ausfuehren(DbPath); return TrainingseinheitenTabelle.Laden(db); }
        public async Task<(GarminImportJobAntwort Job, GarminImportKandidatAntwort Candidate, GarminImportPruefungAntwort Review, GarminImportPruefung Facade)> Prepare(DateTime startUtc)
        {
            using (var db = OeffneDatenbank.Ausfuehren(DbPath))
            using (var tx = db.BeginTransaction())
            {
                AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Rad", IstHaupt = true });
                EinstellungTabelle.Schreiben(db, tx, Einstellungsschluessel.GarminPersonalApiKey, "FAKE-TEST-KEY");
                tx.Commit();
            }
            var jobs = new SqliteGarminImportJobs(DbPath);
            await jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid()));
            var lease = (await jobs.Beanspruchen(Guid.NewGuid(), TimeSpan.FromMinutes(5)))!;
            var candidate = new GarminImportKandidatAntwort(Guid.NewGuid(), 991, GarminImportKandidatStatus.FitBereit, new DateOnly(2026, 4, 1), "stale", 2, 3, null);
            await jobs.DownloadKandidatSpeichern(lease, candidate);
            var job = await jobs.PaketAbschliessen(lease, false, false, []);
            Directory.CreateDirectory(Path.GetDirectoryName(FitPath(job.JobId, candidate.KandidatId))!);
            FitTestdatei.Schreibe(FitPath(job.JobId, candidate.KandidatId), new FitTestdatei.Werte(startUtc, Sensoren: [(1, 123u)]));
            var store = new SqliteGarminImportJobs(DbPath);
            var facade = new GarminImportPruefung(store, _root);
            return (job, candidate, await facade.Laden(job.JobId), facade);
        }
        public void Dispose() { try { Directory.Delete(_root, true); } catch (IOException) { } }
    }
}
