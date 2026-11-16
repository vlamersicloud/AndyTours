using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
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
        Assert.Equal(90.83, row.DistanzKm, 2);
        Assert.Equal(new DateOnly(2026, 4, 1), row.Datum);
        Assert.Equal(64, row.DateiVersion.Length);
        Assert.Equal(f.EquipmentId, row.EmpfohlenesEquipmentId);
        var relative = $"2026/garmin-{candidate.GarminAktivitaetId}-{candidate.KandidatId:N}.fit";
        var permanentPath = f.PermanentPath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(permanentPath)!);
        File.Copy(f.FitPath(job.JobId, candidate.KandidatId), permanentPath);
        var request = new GarminImportUebernahmeAnfrage(job.Version, Guid.NewGuid(), [new GarminImportAuswahl(candidate.KandidatId, row.DateiVersion, true, f.EquipmentId)]);
        var response = await facade.Uebernehmen(job.JobId, request);
        var tour = Assert.Single(response.Ergebnisse);
        Assert.True(tour.Importiert);
        var stored = Assert.Single(f.ReadTours());
        Assert.Equal(2, stored.Anhaenge.Count);
        Assert.Contains(stored.Anhaenge, a => a.RelativerPfad == relative);
        Assert.True(File.Exists(f.PermanentPath(relative)));
        Assert.True(File.Exists(f.FitPath(job.JobId, candidate.KandidatId)));
        Assert.True(File.Exists(f.ZipPath(candidate.GarminAktivitaetId)));
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
        var error = await Assert.ThrowsAsync<StammdatenException>(() => facade.Uebernehmen(job.JobId, request));
        Assert.Equal(StammdatenFehlerart.Konflikt, error.Art);
        Assert.Empty(f.ReadTours());
    }

    [Fact]
    public async Task TimezoneRulesAreBoundToReviewAndRejectStalePreview()
    {
        using var f = new Fixture();
        var (job, candidate, _, _) = await f.Prepare(new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc));
        FitTestdatei.Schreibe(f.FitPath(job.JobId, candidate.KandidatId),
            new FitTestdatei.Werte(new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc), OhneOrtszeit: true));
        var utc = TimeZoneInfo.CreateCustomTimeZone("deterministic-test", TimeSpan.Zero, "test", "test");
        var plusTwo = TimeZoneInfo.CreateCustomTimeZone("deterministic-test", TimeSpan.FromHours(2), "test", "test");
        var reviewA = await f.NewFacade(utc).Laden(job.JobId);
        var reviewBFacade = f.NewFacade(plusTwo);
        var reviewB = await reviewBFacade.Laden(job.JobId);
        var rowA = Assert.Single(reviewA.Zeilen);
        var rowB = Assert.Single(reviewB.Zeilen);
        Assert.Equal(new TimeOnly(8, 0), rowA.Startzeit);
        Assert.Equal(new TimeOnly(10, 0), rowB.Startzeit);
        Assert.Equal(reviewA.FallbackZeitzoneId, reviewB.FallbackZeitzoneId);
        Assert.NotEqual(rowA.DateiVersion, rowB.DateiVersion);
        var request = new GarminImportUebernahmeAnfrage(job.Version, Guid.NewGuid(),
            [new GarminImportAuswahl(candidate.KandidatId, rowA.DateiVersion, true, f.EquipmentId)]);
        var error = await Assert.ThrowsAsync<StammdatenException>(() => reviewBFacade.Uebernehmen(job.JobId, request));
        Assert.Equal(StammdatenFehlerart.Konflikt, error.Art);
        Assert.Empty(f.ReadTours());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "GarminReview-" + Guid.NewGuid().ToString("N"));
        private string DbPath => Path.Combine(_root, "db.sqlite");
        public long EquipmentId { get; private set; }
        public Fixture() { Directory.CreateDirectory(_root); }
        public GarminImportPruefung NewFacade(TimeZoneInfo zone) => new(new SqliteGarminImportJobs(DbPath), _root, zone);
        public string FitPath(Guid jobId, Guid candidateId) => Path.Combine(_root, "GarminImport", jobId.ToString("N"), candidateId.ToString("N") + ".fit");
        public string PermanentPath(string relative) => Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        public string ZipPath(long id) => Path.Combine(_root, "GarminConnect", $"activity-{id}.zip");
        public List<Trainingseinheit> ReadTours() { using var db = OeffneDatenbank.Ausfuehren(DbPath); return TrainingseinheitenTabelle.Laden(db); }
        public async Task<(GarminImportJobAntwort Job, GarminImportKandidatAntwort Candidate, GarminImportPruefungAntwort Review, GarminImportPruefung Facade)> Prepare(DateTime startUtc)
        {
            using (var db = OeffneDatenbank.Ausfuehren(DbPath))
            using (var tx = db.BeginTransaction())
            {
                var activityId = AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Rad", IstHaupt = true });
                EquipmentId = EquipmentsTabelle.Einfuegen(db, tx, new Equipment { Name = "Fixture-Rad", AktivitaetId = activityId, IstStandard = true });
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
            Directory.CreateDirectory(Path.GetDirectoryName(ZipPath(candidate.GarminAktivitaetId))!);
            File.WriteAllBytes(ZipPath(candidate.GarminAktivitaetId), [1, 2, 3, 4]);
            FitTestdatei.Schreibe(FitPath(job.JobId, candidate.KandidatId), new FitTestdatei.Werte(startUtc, Sensoren: [(1, 123u)]));
            var facade = NewFacade(TimeZoneInfo.Local);
            return (job, candidate, await facade.Laden(job.JobId), facade);
        }
        public void Dispose() { try { Directory.Delete(_root, true); } catch (IOException) { } }
    }
}
