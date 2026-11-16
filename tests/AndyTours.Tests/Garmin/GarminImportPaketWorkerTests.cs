using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Garmin;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Import.Garmin;
using Xunit;

namespace AndyTours.Tests.Garmin;

[Collection(FitTestdatei.Testgruppe)]
public sealed class GarminImportPaketWorkerTests
{
    private sealed class Handler(byte[] zip) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? throw new InvalidOperationException();
            Paths.Add(path);
            if (path.Contains("oauth", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("FAKE-TOKEN") });
            if (path.Contains("activities/search/activities", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"activityId\":41},{\"activityId\":42}]", Encoding.UTF8, "application/json")
                });
            if (path.StartsWith("/download-service/files/activity/", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(zip) });
            throw new InvalidOperationException("Unexpected fake HTTP request.");
        }
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "GarminWorkerTest-" + Guid.NewGuid().ToString("N"));
        public string Database => Path.Combine(Root, "andytours.db");
        public SqliteGarminImportJobs Jobs { get; }
        public SqliteGarminImportServerdaten Server { get; }
        public Fixture()
        {
            Directory.CreateDirectory(Root);
            using var db = OeffneDatenbank.Ausfuehren(Database);
            using var tx = db.BeginTransaction();
            AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Fahrrad", IstHaupt = true });
            EinstellungTabelle.Schreiben(db, tx, Einstellungsschluessel.GarminPersonalApiKey, "FAKE-WORKER-KEY");
            tx.Commit();
            Jobs = new SqliteGarminImportJobs(Database);
            Server = new SqliteGarminImportServerdaten(Database);
        }
        public void Dispose() { try { Directory.Delete(Root, true); } catch (IOException) { } }
    }

    [Fact]
    public async Task OhneAuftragKeinPaketUndKeinHttpClient()
    {
        using var f = new Fixture();
        var calls = 0;
        var worker = new GarminImportPaketWorker(f.Jobs, f.Server, new GarminImportArtefakte(f.Root), () =>
        {
            calls++;
            throw new InvalidOperationException("Should not create client without work.");
        });
        Assert.False(await worker.NaechstesPaket(Guid.NewGuid()));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task WorkerLaedtFakeFITsUndPersistiertSichereKandidaten()
    {
        using var f = new Fixture();
        var zip = CreateZip(f.Root);
        Handler? handler = null;
        var worker = new GarminImportPaketWorker(f.Jobs, f.Server, new GarminImportArtefakte(f.Root), () =>
        {
            handler = new Handler(zip);
            return new HttpClient(handler, disposeHandler: true);
        });
        await f.Jobs.Starten(new GarminImportStartAnfrage(Guid.NewGuid()));
        Assert.True(await worker.NaechstesPaket(Guid.NewGuid()));
        var job = (await f.Jobs.AktuellenLaden())!;
        Assert.Equal(GarminImportJobStatus.PaketBereit, job.Status);
        Assert.False(job.WeitereMoeglich);
        Assert.Equal(2, job.Bereit);
        var candidates = (await f.Jobs.Kandidaten(job.JobId, 0, 20)).Kandidaten;
        Assert.Equal(new long[] { 41, 42 }, candidates.Select(candidate => candidate.GarminAktivitaetId));
        Assert.All(candidates, candidate => Assert.Equal(GarminImportKandidatStatus.FitBereit, candidate.Status));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(f.Root, "GarminConnect"), "activity-*.zip").Length);
        var serialized = JsonSerializer.Serialize(job) + JsonSerializer.Serialize(candidates);
        Assert.DoesNotContain("FAKE-WORKER-KEY", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(f.Root, serialized, StringComparison.Ordinal);
        Assert.NotNull(handler);
        Assert.Equal(2, handler!.Paths.Count(path => path.StartsWith("/download-service/files/activity/", StringComparison.Ordinal)));
    }

    private static byte[] CreateZip(string root)
    {
        var fit = Path.Combine(root, "synthetic.fit");
        FitTestdatei.Schreibe(fit, new FitTestdatei.Werte(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("activity.fit");
            using var target = entry.Open();
            using var source = File.OpenRead(fit);
            source.CopyTo(target);
        }
        File.Delete(fit);
        return output.ToArray();
    }
}
