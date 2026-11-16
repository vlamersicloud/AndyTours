using System.Text.Json;
using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndyTours.Tests.Daten;

public sealed class SqliteGarminImportJobsTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; private set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(TimeSpan duration) => Now = Now.Add(duration);
    }

    private sealed class Fixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GarminJobs-" + Guid.NewGuid().ToString("N") + ".db");
        public Clock Time { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        public SqliteGarminImportJobs Jobs { get; }
        public Fixture(bool key = true, bool main = true)
        {
            using var db = OeffneDatenbank.Ausfuehren(Path);
            using var tx = db.BeginTransaction();
            if (main) AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Fahrrad", IstHaupt = true });
            if (key) EinstellungTabelle.Schreiben(db, tx, Einstellungsschluessel.GarminPersonalApiKey, "FAKE-TEST-KEY");
            tx.Commit();
            Jobs = new SqliteGarminImportJobs(Path, Time);
        }
        public SqliteGarminImportJobs Another() => new(Path, Time);
        public void Dispose() { try { File.Delete(Path); } catch (IOException) { } }
    }

    [Fact]
    public async Task StartIstIdempotentUndZweiterJobKonfligiert()
    {
        using var f = new Fixture();
        var request = new GarminImportStartAnfrage(Guid.NewGuid());
        var first = await f.Jobs.Starten(request);
        var replay = await f.Another().Starten(request);
        Assert.Equal(first.JobId, replay.JobId);
        var ex = await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid())));
        Assert.Equal(StammdatenFehlerart.Konflikt, ex.Art);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task FehlendeVoraussetzungErzeugtKeinenJob(bool key, bool main)
    {
        using var f = new Fixture(key, main);
        await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid())));
        Assert.Null(await f.Jobs.AktuellenLaden());
    }

    [Fact]
    public async Task AbbrechenBewahrtKandidatenUndWiederholungLiefertQuittung()
    {
        using var f = new Fixture();
        var job = await f.Jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid()));
        InsertCandidate(f.Path, job.JobId, 111, 1);
        var control = new GarminImportSteuerAnfrage(job.Version, Guid.NewGuid());
        var canceled = await f.Jobs.Abbrechen(job.JobId, control);
        var replay = await f.Jobs.Abbrechen(job.JobId, control);
        Assert.Equal(canceled.JobId, replay.JobId);
        Assert.Equal(canceled.Version, replay.Version);
        Assert.Equal(canceled.Status, replay.Status);
        Assert.Equal(canceled.Warncodes, replay.Warncodes);
        Assert.NotEqual(job.Version, canceled.Version);
        Assert.Equal(1, (await f.Jobs.Kandidaten(job.JobId, 0, 10)).Gesamt);
    }

    [Fact]
    public async Task AnfrageIdMitAndererAktionKonfligiert()
    {
        using var f = new Fixture();
        var job = await f.Jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid()));
        var control = new GarminImportSteuerAnfrage(job.Version, Guid.NewGuid());
        await f.Jobs.Abbrechen(job.JobId, control);
        var ex = await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.Fortsetzen(job.JobId, control));
        Assert.Equal(StammdatenFehlerart.Konflikt, ex.Art);
    }

    [Fact]
    public async Task KonkurrierenderJobBlockiertFortsetzenAbgebrochenenJobs()
    {
        using var f = new Fixture();
        var first = await f.Jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid()));
        var canceled = await f.Jobs.Abbrechen(first.JobId, new GarminImportSteuerAnfrage(first.Version, Guid.NewGuid()));
        var second = await f.Jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid()));
        var ex = await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.Fortsetzen(first.JobId, new GarminImportSteuerAnfrage(canceled.Version, Guid.NewGuid())));
        Assert.Equal(StammdatenFehlerart.Konflikt, ex.Art);
        Assert.Equal(second.JobId, (await f.Jobs.AktuellenLaden())!.JobId);
    }

    [Fact]
    public async Task AktuellerJobUndKandidatenZaehlerSindKorrektUndFertigerWirdAusgeschlossen()
    {
        using var f = new Fixture();
        var job = await f.Jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid()));
        InsertCandidate(f.Path, job.JobId, 1, 1);
        InsertCandidate(f.Path, job.JobId, 2, 2);
        InsertCandidate(f.Path, job.JobId, 3, 3);
        var current = await f.Jobs.AktuellenLaden();
        Assert.Equal(job.JobId, current!.JobId);
        Assert.Equal(3, current.Bearbeitet);
        Assert.Equal(1, current.Bereit);
        Assert.Equal(1, current.Importiert);
        SetJobState(f.Path, job.JobId, 5);
        Assert.Null(await f.Jobs.AktuellenLaden());
    }

    [Fact]
    public async Task LeaseWirdNurEinmalBeanspruchtUndNachAblaufGefenced()
    {
        using var f = new Fixture();
        await f.Jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid()));
        var owner = Guid.NewGuid();
        var lease = await f.Jobs.Beanspruchen(owner, TimeSpan.FromSeconds(5));
        Assert.NotNull(lease);
        Assert.Null(await f.Another().Beanspruchen(Guid.NewGuid(), TimeSpan.FromSeconds(5)));
        f.Time.Advance(TimeSpan.FromSeconds(6));
        var renewedOwner = Guid.NewGuid();
        var replacement = await f.Another().Beanspruchen(renewedOwner, TimeSpan.FromSeconds(10));
        Assert.NotNull(replacement);
        Assert.True(replacement!.Generation > lease!.Generation);
        Assert.Null(await f.Jobs.Verlaengern(lease, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task AbbruchWiderruftLeaseUndUngueltigeDauerOderOwnerWerdenAbgelehnt()
    {
        using var f = new Fixture();
        var job = await f.Jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid()));
        var lease = await f.Jobs.Beanspruchen(Guid.NewGuid(), TimeSpan.FromSeconds(10));
        var live = await f.Jobs.Laden(job.JobId);
        await f.Jobs.Abbrechen(job.JobId, new GarminImportSteuerAnfrage(live!.Version, Guid.NewGuid()));
        Assert.Null(await f.Jobs.Verlaengern(lease!, TimeSpan.FromSeconds(10)));
        await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.Beanspruchen(Guid.Empty, TimeSpan.FromSeconds(10)));
        await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.Beanspruchen(Guid.NewGuid(), TimeSpan.FromSeconds(4)));
    }

    [Fact]
    public async Task AbgebrochenerAufrufBeanspruchtKeineLease()
    {
        using var f = new Fixture();
        await f.Jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Jobs.Beanspruchen(Guid.NewGuid(), TimeSpan.FromSeconds(10), cancellation.Token));
        Assert.Equal(GarminImportJobStatus.Wartend, (await f.Jobs.AktuellenLaden())!.Status);
    }

    private static void SetJobState(string path, Guid job, int state)
    {
        using var db = OeffneDatenbank.Ausfuehren(path);
        using var command = db.CreateCommand();
        command.CommandText = "UPDATE GarminImportJob SET Zustand=$state WHERE JobId=$job;";
        command.Parameters.AddWithValue("$state", state);
        command.Parameters.AddWithValue("$job", job.ToString("D"));
        command.ExecuteNonQuery();
    }

    private static void InsertCandidate(string path, Guid job, long activity, int state)
    {
        using var db = OeffneDatenbank.Ausfuehren(path);
        using var command = db.CreateCommand();
        command.CommandText = "INSERT INTO GarminImportKandidat(KandidatId,JobId,GarminAktivitaetId,Paketnummer,Zustand,Name) VALUES($id,$job,$activity,1,$state,'fixture');";
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$job", job.ToString("D"));
        command.Parameters.AddWithValue("$activity", activity);
        command.Parameters.AddWithValue("$state", state);
        command.ExecuteNonQuery();
    }
}
