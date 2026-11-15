using System.IO.Compression;
using System.Net;
using System.Text;
using AndyTours.Import.Garmin;

namespace AndyTours.Tests.Garmin;

/// <summary>Regressionen für kleine Pakete, verzögerte Seiten und importierbare Ergebnisse nach Netzfehlern.</summary>
public sealed class GarminDownloadPaketTests
{
    [Fact]
    public async Task NaechsteSeiteWirdErstBeimNaechstenPaketGeladen()
    {
        string ordner = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string archive = Path.Combine(ordner, "archiv");
        Directory.CreateDirectory(archive);
        for (int id = 1; id <= 21; id++)
        {
            File.WriteAllBytes(Path.Combine(archive, $"activity-{id}.zip"), Archiv());
        }

        int seiten = 0;
        using var handler = new AntwortHandler(anfrage =>
        {
            if (anfrage.Method == HttpMethod.Post) { return Text("token"); }
            seiten++;
            Assert.Contains("limit=20&", anfrage.RequestUri!.Query);
            return Text(anfrage.RequestUri.Query.Contains("start=0&", StringComparison.Ordinal)
                ? "[" + string.Join(",", Enumerable.Range(1, 20).Select(id => $"{{\"activityId\":{id}}}")) + "]"
                : "[{\"activityId\":21}]");
        });
        using var http = new HttpClient(handler);
        var client = new GarminPersonalClient(http, "test-key");
        try
        {
            await using var ids = client.LeseRadaktivitaetsIdsAsync().GetAsyncEnumerator();
            GarminDownloadPaket erstes = await LadeGarminConnectAufzeichnungen.LadePaketAsync(
                client, ids, archive, Path.Combine(ordner, "temp"), new HashSet<long>());
            Assert.Equal(20, erstes.Dateien.Count);
            Assert.True(erstes.WeitereMoeglich);
            Assert.False(erstes.Unterbrochen);
            Assert.Equal(1, seiten);

            GarminDownloadPaket zweites = await LadeGarminConnectAufzeichnungen.LadePaketAsync(
                client, ids, archive, Path.Combine(ordner, "temp"), new HashSet<long>());
            Assert.Single(zweites.Dateien);
            Assert.Equal(21L, zweites.Dateien[0].Id);
            Assert.False(zweites.WeitereMoeglich);
            Assert.False(zweites.Unterbrochen);
            Assert.Equal(2, seiten);
        }
        finally { Directory.Delete(ordner, true); }
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task AbbruchLaesstFertigeDateienImportierbarUndEntferntTeilDownload(HttpStatusCode status)
    {
        string ordner = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string archive = Path.Combine(ordner, "archiv");
        Directory.CreateDirectory(archive);
        File.WriteAllBytes(Path.Combine(archive, "activity-1.zip"), Archiv());
        File.WriteAllBytes(Path.Combine(archive, "activity-2.zip"), Archiv());
        int downloads = 0;
        var warnungen = new Warnungen();
        using var handler = new AntwortHandler(anfrage =>
        {
            if (anfrage.Method == HttpMethod.Post) { return Text("token"); }
            if (anfrage.RequestUri!.AbsolutePath.Contains("activitylist", StringComparison.Ordinal))
            {
                return Text("[{\"activityId\":1},{\"activityId\":2},{\"activityId\":3}]");
            }

            downloads++;
            return new HttpResponseMessage(status);
        });
        using var http = new HttpClient(handler);
        var client = new GarminPersonalClient(http, "test-key");
        try
        {
            await using var ids = client.LeseRadaktivitaetsIdsAsync().GetAsyncEnumerator();
            GarminDownloadPaket paket = await LadeGarminConnectAufzeichnungen.LadePaketAsync(
                client, ids, archive, Path.Combine(ordner, "temp"), new HashSet<long>(), warnungen: warnungen);
            Assert.True(paket.Unterbrochen);
            Assert.False(paket.WeitereMoeglich);
            Assert.Equal(2, paket.Dateien.Count);
            Assert.All(paket.Dateien, datei => Assert.True(File.Exists(datei.Pfad)));
            Assert.Single(warnungen.Texte);
            Assert.Contains("unterbrochen", warnungen.Texte[0]);
            Assert.Equal(1, downloads); // kein Retry-Sturm bei 429/503
            Assert.Empty(Directory.GetFiles(archive, "*.tmp"));
            Assert.Equal(2, Directory.GetFiles(archive, "*.zip").Length);
        }
        finally { Directory.Delete(ordner, true); }
    }

    [Fact]
    public async Task FehlerBeimListenabrufBeendetPaketMitHinweis()
    {
        string ordner = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using var handler = new AntwortHandler(anfrage => anfrage.Method == HttpMethod.Post
            ? Text("token") : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var http = new HttpClient(handler);
        var client = new GarminPersonalClient(http, "test-key");
        var warnungen = new Warnungen();
        try
        {
            await using var ids = client.LeseRadaktivitaetsIdsAsync().GetAsyncEnumerator();
            GarminDownloadPaket paket = await LadeGarminConnectAufzeichnungen.LadePaketAsync(
                client, ids, Path.Combine(ordner, "archiv"), Path.Combine(ordner, "temp"),
                new HashSet<long>(), warnungen: warnungen);
            Assert.True(paket.Unterbrochen);
            Assert.Empty(paket.Dateien);
            Assert.Single(warnungen.Texte);
        }
        finally { Directory.Delete(ordner, true); }
    }

    private static byte[] Archiv()
    {
        using var daten = new MemoryStream();
        using (var zip = new ZipArchive(daten, ZipArchiveMode.Create, true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("original.fit").Open(), Encoding.UTF8);
            writer.Write("test-fit");
        }

        return daten.ToArray();
    }

    private static HttpResponseMessage Text(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text) };

    private sealed class AntwortHandler(Func<HttpRequestMessage, HttpResponseMessage> antwort) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(antwort(request));
    }

    private sealed class Warnungen : IProgress<string>
    {
        public List<string> Texte { get; } = [];
        public void Report(string value) => Texte.Add(value);
    }
}
