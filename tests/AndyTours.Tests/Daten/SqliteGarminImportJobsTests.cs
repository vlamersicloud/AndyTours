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
    private sealed class Fixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GarminJobs-" + Guid.NewGuid().ToString("N") + ".db");
        public Fixture(bool key = true, bool main = true)
        {
            using var db = OeffneDatenbank.Ausfuehren(Path);
            using var tx = db.BeginTransaction();
            if (main) AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Fahrrad", IstHaupt = true });
            if (key) EinstellungTabelle.Schreiben(db, tx, Einstellungsschluessel.GarminPersonalApiKey, "FAKE-TEST-KEY");
            tx.Commit();
            Jobs = new SqliteGarminImportJobs(Path);
        }
        public SqliteGarminImportJobs Jobs { get; }
        public void Dispose() { try { File.Delete(Path); } catch (IOException) { } }
    }

    [Fact]
    public async Task StartIstIdempotentUndZweiterJobKonfligiert()
    {
        using var f = new Fixture();
        var request = new GarminImportStartAnfrage(Guid.NewGuid());
        var first = await f.Jobs.Starten(request);
        var replay = await new SqliteGarminImportJobs(f.Path).Starten(request);
        Assert.Equal(first.JobId, replay.JobId);
        var ex = await Assert.ThrowsAsync<StammdatenException>(() => f.Jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid())));
        Assert.Equal(StammdatenFehlerart.Konflikt, ex.Art);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task FehlenderSchluesselOderHauptaktivitaetErzeugtKeinenJob(bool key, bool main)
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
        var candidate = Guid.NewGuid();
        using (var db = OeffneDatenbank.Ausfuehren(f.Path))
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO GarminImportKandidat(KandidatId,JobId,GarminAktivitaetId,Paketnummer,Zustand,Name) VALUES($id,$job,111,1,1,'fixture');";
            cmd.Parameters.AddWithValue("$id", candidate.ToString("D")); cmd.Parameters.AddWithValue("$job", job.JobId.ToString("D")); cmd.ExecuteNonQuery();
        }
        var control = new GarminImportSteuerAnfrage(job.Version, Guid.NewGuid());
        var canceled = await f.Jobs.Abbrechen(job.JobId, control);
        Assert.NotEqual(job.Version, canceled.Version);
        Assert.Equal(canceled, await f.Jobs.Abbrechen(job.JobId, control));
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
    public async Task FortsetzenSetztAbgebrochenenJobZurueckUndAktiverJobBlockiertWeiteren()
    {
        using var f = new Fixture();
        var first = await f.Jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid()));
        var canceled = await f.Jobs.Abbrechen(first.JobId, new GarminImportSteuerAnfrage(first.Version, Guid.NewGuid()));
        var resumed = await f.Jobs.Fortsetzen(first.JobId, new GarminImportSteuerAnfrage(canceled.Version, Guid.NewGuid()));
        Assert.Equal(GarminImportJobStatus.Wartend, resumed.Status);
    }

    [Fact]
    public async Task AktuellerJobUndKandidatenZaehlerSindKorrekt()
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
    }

    private static void InsertCandidate(string path, Guid job, long activity, int state)
    {
        using var db = OeffneDatenbank.Ausfuehren(path);
        using var command = db.CreateCommand();
        command.CommandText = "INSERT INTO GarminImportKandidat(KandidatId,JobId,GarminAktivitaetId,Paketnummer,Zustand,Name) VALUES($id,$job,$activity,1,$state,'fixture');";
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D")); command.Parameters.AddWithValue("$job", job.ToString("D"));
        command.Parameters.AddWithValue("$activity", activity); command.Parameters.AddWithValue("$state", state); command.ExecuteNonQuery();
    }
}
