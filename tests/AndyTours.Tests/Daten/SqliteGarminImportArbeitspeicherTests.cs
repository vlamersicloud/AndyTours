using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Xunit;

namespace AndyTours.Tests.Daten;

public sealed class SqliteGarminImportArbeitspeicherTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; private set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(TimeSpan by) => Now = Now.Add(by);
    }

    private sealed class Fixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GarminWorker-" + Guid.NewGuid().ToString("N") + ".db");
        public Clock Time { get; } = new();
        public SqliteGarminImportJobs Jobs { get; }
        public Fixture(bool automatic = false, int count = 0)
        {
            using var db = OeffneDatenbank.Ausfuehren(Path);
            using var tx = db.BeginTransaction();
            AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Fahrrad", IstHaupt = true });
            EinstellungTabelle.Schreiben(db, tx, Einstellungsschluessel.GarminPersonalApiKey, "FAKE-ONLY-TEST");
            EinstellungTabelle.Schreiben(db, tx, Einstellungsschluessel.GarminAllePaketeAutomatisch, automatic ? "true" : null);
            tx.Commit();
            Jobs = new SqliteGarminImportJobs(Path, Time);
            Job = Jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid())).GetAwaiter().GetResult();
            if (count > 0)
            {
                var lease = Jobs.Beanspruchen(Guid.NewGuid(), TimeSpan.FromMinutes(1)).GetAwaiter().GetResult()!;
                for (var i = 1; i <= count; i++) Save(lease, Candidate(i));
                Lease = lease;
            }
        }
        public GarminImportJobAntwort Job { get; }
        public GarminImportLease? Lease { get; private set; }
        public static GarminImportKandidatAntwort Candidate(long id, GarminImportKandidatStatus status = GarminImportKandidatStatus.Wartend) =>
            new(Guid.NewGuid(), id, status, new DateOnly(2026, 1, 2), "Tour " + id, 20.5, 3600, null);
        public GarminImportKandidatAntwort Save(GarminImportLease lease, GarminImportKandidatAntwort candidate) =>
            Jobs.DownloadKandidatSpeichern(lease, candidate).GetAwaiter().GetResult();
        public GarminImportLease Claim() => Jobs.Beanspruchen(Guid.NewGuid(), TimeSpan.FromMinutes(1)).GetAwaiter().GetResult()!;
        public void Dispose() { try { File.Delete(Path); } catch (IOException) { } }
    }

    [Fact]
    public async Task SpeichernLiefertGarminIdUndStabileKandidatenIdMitStatusfortschritt()
    {
        using var f = new Fixture();
        var lease = f.Claim();
        var candidate = Fixture.Candidate(601);
        var saved = await f.Jobs.DownloadKandidatSpeichern(lease, candidate with { Status = GarminImportKandidatStatus.ZipBereit });
        var advanced = await f.Jobs.DownloadKandidatSpeichern(lease, saved with { Status = GarminImportKandidatStatus.FitBereit });
        Assert.Equal(601, advanced.GarminAktivitaetId);
        Assert.Equal(candidate.KandidatId, advanced.KandidatId);
        Assert.Equal(GarminImportKandidatStatus.FitBereit, advanced.Status);
    }

    [Fact]
    public async Task FitBereitKannNichtZurueckgestuftWerden()
    {
        using var f = new Fixture();
        var lease = f.Claim();
        var fit = await f.Jobs.DownloadKandidatSpeichern(lease, Fixture.Candidate(602, GarminImportKandidatStatus.FitBereit));
        var replay = await f.Jobs.DownloadKandidatSpeichern(lease, fit with { Status = GarminImportKandidatStatus.ZipBereit });
        Assert.Equal(GarminImportKandidatStatus.FitBereit, replay.Status);
        Assert.Equal(fit.KandidatId, replay.KandidatId);
    }

    [Fact]
    public async Task AbweichendeKandidatenIdWirdAbgelehnt()
    {
        using var f = new Fixture();
        var lease = f.Claim();
        var saved = await f.Jobs.DownloadKandidatSpeichern(lease, Fixture.Candidate(603));
        await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.DownloadKandidatSpeichern(lease, saved with { KandidatId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task EinUndZwanzigsterKandidatWirdVorDemSchreibenAbgelehnt()
    {
        using var f = new Fixture();
        var lease = f.Claim();
        for (var i = 1; i <= 20; i++) await f.Jobs.DownloadKandidatSpeichern(lease, Fixture.Candidate(i));
        await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.DownloadKandidatSpeichern(lease, Fixture.Candidate(21)));
        Assert.Equal(20, (await f.Jobs.BeobachteteKandidaten(lease)).Count);
    }

    [Fact]
    public async Task VeralteteAbgelaufeneUndWiderrufeneLeaseKoennenNichtSchreiben()
    {
        using var f = new Fixture();
        var first = f.Claim();
        f.Time.Advance(TimeSpan.FromMinutes(2));
        var replacement = f.Claim();
        await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.DownloadKandidatSpeichern(first, Fixture.Candidate(604)));
        await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.BeobachteteKandidaten(first));
        await f.Jobs.Abbrechen(f.Job.JobId, new GarminImportSteuerAnfrage(replacement.Auftrag.Version, Guid.NewGuid()));
        await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.PaketAbschliessen(replacement, false, false, []));
        Assert.Empty(await f.Jobs.Kandidaten(f.Job.JobId, 0, 20).ContinueWith(x => x.Result.Kandidaten));
    }

    [Fact]
    public async Task ManuellMitWeiterePaketeWirdPaketBereitUndFitPruefungBlockiertFortsetzen()
    {
        using var f = new Fixture();
        var lease = f.Claim();
        await f.Jobs.DownloadKandidatSpeichern(lease, Fixture.Candidate(605, GarminImportKandidatStatus.FitBereit));
        var completed = await f.Jobs.PaketAbschliessen(lease, false, true, []);
        Assert.Equal(GarminImportJobStatus.PaketBereit, completed.Status);
        Assert.Equal(1, completed.Bereit);
        await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.Fortsetzen(completed.JobId,
            new GarminImportSteuerAnfrage(completed.Version, Guid.NewGuid())));
    }

    [Fact]
    public async Task AutomatikQueueErhoehtPaketUndBewahrtVorherigeKandidaten()
    {
        using var f = new Fixture(automatic: true);
        var lease = f.Claim();
        await f.Jobs.DownloadKandidatSpeichern(lease, Fixture.Candidate(606, GarminImportKandidatStatus.FitBereit));
        var completed = await f.Jobs.PaketAbschliessen(lease, false, true, []);
        Assert.Equal(2, completed.Paketnummer);
        Assert.Equal(GarminImportJobStatus.Wartend, completed.Status);
        Assert.Single(await f.Jobs.BeobachteteKandidaten(f.Claim()));
    }

    [Fact]
    public async Task KeineWeiterenPaketeMitFitKandidatBleibtReviewBereitSonstFertig()
    {
        using (var f = new Fixture())
        {
            var lease = f.Claim();
            await f.Jobs.DownloadKandidatSpeichern(lease, Fixture.Candidate(607, GarminImportKandidatStatus.FitBereit));
            var done = await f.Jobs.PaketAbschliessen(lease, false, false, []);
            Assert.Equal(GarminImportJobStatus.PaketBereit, done.Status);
        }
        using (var f = new Fixture())
        {
            var lease = f.Claim();
            var done = await f.Jobs.PaketAbschliessen(lease, false, false, []);
            Assert.Equal(GarminImportJobStatus.Fertig, done.Status);
            Assert.Null(await f.Jobs.AktuellenLaden());
        }
    }

    [Fact]
    public async Task UnterbrechungBewahrtPaketUndKannFortgesetztWerden()
    {
        using var f = new Fixture();
        var first = f.Claim();
        var packet = first.Auftrag.Paketnummer;
        var paused = await f.Jobs.PaketAbschliessen(first, true, true, [GarminImportWarncodes.Netzwerk]);
        Assert.Equal(GarminImportJobStatus.Unterbrochen, paused.Status);
        Assert.Equal(packet, paused.Paketnummer);
        var resumed = await f.Jobs.Fortsetzen(paused.JobId, new GarminImportSteuerAnfrage(paused.Version, Guid.NewGuid()));
        Assert.Equal(GarminImportJobStatus.Wartend, resumed.Status);
        Assert.Equal(packet, resumed.Paketnummer);
    }

    [Fact]
    public async Task UnbekannteWarncodesUndUngueltigeKandidatendatenWerdenAbgelehnt()
    {
        using var f = new Fixture();
        var lease = f.Claim();
        await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.PaketAbschliessen(lease, false, false, ["exception text"]));
        await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.DownloadKandidatSpeichern(lease, Fixture.Candidate(608) with { DistanzKm = double.NaN }));
        await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.DownloadKandidatSpeichern(lease, Fixture.Candidate(609) with { FahrzeitSekunden = -1 }));
    }
    [Theory]
    [InlineData(GarminImportKandidatStatus.Wartend)]
    [InlineData(GarminImportKandidatStatus.ZipBereit)]
    public async Task ErfolgreicherPaketabschlussLehntUnfertigeKandidatenAb(GarminImportKandidatStatus status)
    {
        using var f = new Fixture();
        var lease = f.Claim();
        var candidate = await f.Jobs.DownloadKandidatSpeichern(lease, Fixture.Candidate(701, status));
        var packet = lease.Auftrag.Paketnummer;
        await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.PaketAbschliessen(lease, false, true, []));
        var stillRunning = await f.Jobs.Laden(lease.JobId);
        Assert.Equal(GarminImportJobStatus.Laeuft, stillRunning!.Status);
        Assert.Equal(packet, stillRunning.Paketnummer);
        var observed = await f.Jobs.BeobachteteKandidaten(lease);
        Assert.Single(observed);
        Assert.Equal(packet, observed[0].Paketnummer);
        Assert.Equal(candidate.KandidatId, observed[0].Kandidat.KandidatId);
        Assert.Equal(status, observed[0].Kandidat.Status);
        var interrupted = await f.Jobs.PaketAbschliessen(lease, true, true, [GarminImportWarncodes.Abgebrochen]);
        Assert.Equal(GarminImportJobStatus.Unterbrochen, interrupted.Status);
        Assert.Equal(packet, interrupted.Paketnummer);
    }

    [Fact]
    public async Task ServerkontextGibtNurZuNochVorhandenenTourenPassendeZuordnungenZurueck()
    {
        using var fixture = new Fixture();
        long existingTour;
        long deletedTour;
        using (var db = OeffneDatenbank.Ausfuehren(fixture.Path))
        {
            var first = new AndyTours.Core.Modell.Trainingseinheit
            {
                Datum = new DateOnly(2025, 4, 1), Name = "fixture", AktivitaetId = 1
            };
            var second = new AndyTours.Core.Modell.Trainingseinheit
            {
                Datum = new DateOnly(2025, 4, 2), Name = "deleted", AktivitaetId = 1
            };
            existingTour = TrainingseinheitenTabelle.Einfuegen(db, first).Id;
            deletedTour = TrainingseinheitenTabelle.Einfuegen(db, second).Id;
            using var mapping = db.CreateCommand();
            mapping.CommandText = "INSERT OR REPLACE INTO Einstellung(Schluessel,Wert) VALUES($key,$value);";
            mapping.Parameters.AddWithValue("$key", "GarminConnect.Aktivitaet.801");
            mapping.Parameters.AddWithValue("$value", existingTour.ToString(System.Globalization.CultureInfo.InvariantCulture));
            mapping.ExecuteNonQuery();
            mapping.Parameters.Clear();
            mapping.Parameters.AddWithValue("$key", "GarminConnect.Aktivitaet.802");
            mapping.Parameters.AddWithValue("$value", deletedTour.ToString(System.Globalization.CultureInfo.InvariantCulture));
            mapping.ExecuteNonQuery();
            using var remove = db.CreateCommand();
            remove.CommandText = "DELETE FROM Trainingseinheit WHERE Id=$id;";
            remove.Parameters.AddWithValue("$id", deletedTour);
            remove.ExecuteNonQuery();
        }
        var context = await new SqliteGarminImportServerdaten(fixture.Path).Laden();
        Assert.Contains(801, context.BereitsImportiert);
        Assert.DoesNotContain(802, context.BereitsImportiert);
        Assert.DoesNotContain("FAKE-ONLY-TEST", context.ToString());
    }

    [Fact]
    public async Task FehlenderServerKeyIstSichererDomainfehler()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GarminNoKey-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var db = OeffneDatenbank.Ausfuehren(path)) { }
            var ex = await Assert.ThrowsAsync<StammdatenException>(() => new SqliteGarminImportServerdaten(path).Laden());
            Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
            Assert.DoesNotContain("key", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { try { File.Delete(path); } catch (IOException) { } }
    }

}
