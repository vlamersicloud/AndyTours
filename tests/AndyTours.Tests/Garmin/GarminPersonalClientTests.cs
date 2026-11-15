using System.IO.Compression;
using System.Net;
using System.Text;
using AndyTours.Import.Garmin;

namespace AndyTours.Tests.Garmin;

/// <summary>Offline-Vertragstests: kein API-Key, kein Konto und kein Netzwerk erforderlich.</summary>
public sealed class GarminPersonalClientTests
{
    [Fact]
    public async Task AuthentifizierungPagingUndDownloadVerwendenOffizielleEndpunkte()
    {
        var anfragen = new List<string>();
        using var handler = new AntwortHandler(anfrage =>
        {
            string pfad = anfrage.RequestUri!.PathAndQuery;
            anfragen.Add(pfad);
            Assert.Equal("personal.connectapi.garmin.com", anfrage.RequestUri.Host);
            if (anfrage.Method == HttpMethod.Post)
            {
                Assert.Equal("ApiKey test-key", anfrage.Headers.Authorization!.ToString());
                return Text("test-token");
            }

            Assert.Equal("Bearer test-token", anfrage.Headers.Authorization!.ToString());
            if (pfad.Contains("start=0&", StringComparison.Ordinal))
            {
                return Text("[" + string.Join(",", Enumerable.Range(1, 100).Select(id => $"{{\"activityId\":{id}}}")) + "]");
            }

            if (pfad.Contains("start=100&", StringComparison.Ordinal))
            {
                return Text("[{\"activityId\":101}]");
            }

            Assert.Equal("/download-service/files/activity/101", pfad);
            return Text("zip-bytes");
        });
        using var http = new HttpClient(handler);
        var client = new GarminPersonalClient(http, "test-key");
        IReadOnlyList<long> ids = await client.LeseRadaktivitaetenAsync();
        Assert.Equal(101, ids.Count);
        using var ausgabe = new MemoryStream();
        await client.LadeArchivAsync(101, ausgabe);
        Assert.Equal("zip-bytes", Encoding.UTF8.GetString(ausgabe.ToArray()));
        Assert.Equal(4, anfragen.Count);
    }

    [Fact]
    public async Task UnauthorizedErneuertTokenGenauEinmal()
    {
        int tokens = 0, aufrufe = 0;
        using var handler = new AntwortHandler(anfrage =>
        {
            if (anfrage.Method == HttpMethod.Post)
            {
                return Text("token-" + ++tokens);
            }

            aufrufe++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });
        using var http = new HttpClient(handler);
        var client = new GarminPersonalClient(http, "secret");
        await Assert.ThrowsAsync<HttpRequestException>(() => client.LeseRadaktivitaetenAsync());
        Assert.Equal(2, tokens);
        Assert.Equal(2, aufrufe);
    }

    [Fact]
    public async Task RatenlimitWirdNichtWiederholtUndGibtKeineSecretsAus()
    {
        int aufrufe = 0;
        using var handler = new AntwortHandler(anfrage =>
        {
            aufrufe++;
            return anfrage.Method == HttpMethod.Post ? Text("token-secret")
                : new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("key-secret") };
        });
        using var http = new HttpClient(handler);
        var client = new GarminPersonalClient(http, "key-secret");
        HttpRequestException fehler = await Assert.ThrowsAsync<HttpRequestException>(() => client.LeseRadaktivitaetenAsync());
        Assert.Contains("429", fehler.Message);
        Assert.DoesNotContain("secret", fehler.Message);
        Assert.Equal(2, aufrufe);
    }

    [Fact]
    public async Task WiederholteVolleSeiteBrichtAb()
    {
        string seite = "[" + string.Join(",", Enumerable.Range(1, 100).Select(id => $"{{\"activityId\":{id}}}")) + "]";
        using var handler = new AntwortHandler(anfrage => Text(anfrage.Method == HttpMethod.Post ? "token" : seite));
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => new GarminPersonalClient(http, "key").LeseRadaktivitaetenAsync());
    }

    [Fact]
    public async Task ArchivcacheVerhindertErneutenDownloadUndBekannteIdsWerdenUebersprungen()
    {
        string ordner = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        int downloads = 0;
        using var handler = new AntwortHandler(anfrage =>
        {
            if (anfrage.Method == HttpMethod.Post) { return Text("token"); }
            if (anfrage.RequestUri!.AbsolutePath.Contains("activitylist", StringComparison.Ordinal))
            {
                return Text("[{\"activityId\":11},{\"activityId\":12}]");
            }

            downloads++;
            using var daten = new MemoryStream();
            using (var zip = new ZipArchive(daten, ZipArchiveMode.Create, true))
            {
                using var writer = new StreamWriter(zip.CreateEntry("../../original.fit").Open());
                writer.Write("fit-data");
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(daten.ToArray()) };
        });
        using var http = new HttpClient(handler);
        var client = new GarminPersonalClient(http, "key");
        try
        {
            var a = await LadeGarminConnectAufzeichnungen.AusfuehrenAsync(client, Path.Combine(ordner, "archiv"), Path.Combine(ordner, "temp"), new HashSet<long> { 11 });
            Assert.Single(a);
            Assert.Equal(12, a[0].Id);
            Assert.Equal("fit-data", File.ReadAllText(a[0].Pfad));
            await LadeGarminConnectAufzeichnungen.AusfuehrenAsync(client, Path.Combine(ordner, "archiv"), Path.Combine(ordner, "temp"), new HashSet<long> { 11 });
            Assert.Equal(1, downloads);
        }
        finally { Directory.Delete(ordner, true); }
    }

    [Fact]
    public async Task MehrdeutigesArchivWirdAbgelehnt()
    {
        string ordner = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ordner);
        try
        {
            string archiv = Path.Combine(ordner, "test.zip");
            using (var zip = ZipFile.Open(archiv, ZipArchiveMode.Create))
            {
                zip.CreateEntry("a.fit");
                zip.CreateEntry("b.fit");
            }

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                LadeGarminConnectAufzeichnungen.EntpackeFitAsync(archiv, Path.Combine(ordner, "ziel.fit")));
        }
        finally { Directory.Delete(ordner, true); }
    }

    private static HttpResponseMessage Text(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text) };

    private sealed class AntwortHandler(Func<HttpRequestMessage, HttpResponseMessage> antwort) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(antwort(request));
    }
}
