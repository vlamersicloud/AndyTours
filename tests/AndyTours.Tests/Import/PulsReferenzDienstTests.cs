using AndyTours.Application.Auswertung;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Import.Auswertung;
using Microsoft.Data.Sqlite;
using System.Net;
using System.Text;

namespace AndyTours.Tests.Import;

public sealed class PulsReferenzDienstTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PulseRefresh-" + Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(_root, "db.sqlite");

    public PulsReferenzDienstTests()
    {
        Directory.CreateDirectory(_root);
        using var db = OeffneDatenbank.Ausfuehren(Database);
    }

    [Fact]
    public async Task MissingKeyIsOfflineAndSuccessfulRefreshStoresCurrentReference()
    {
        var source = new SqlitePulsQuelldaten(Database);
        var creates = 0;
        var offline = new PulsAuswertungDienst(source, _root, () =>
        {
            creates++;
            throw new InvalidOperationException("must stay offline");
        });
        var read = await offline.LadenReferenz(CancellationToken.None);
        Assert.Null(read.Abgerufen);
        Assert.Empty(read.Profile);
        await Assert.ThrowsAsync<AndyTours.Application.Stammdaten.StammdatenException>(() =>
            offline.AktualisierenReferenz(new(read.Version), CancellationToken.None));
        Assert.Equal(0, creates);

        const string key = "FAKE-DB-KEY";
        using (var db = OeffneDatenbank.Ausfuehren(Database)) GarminEinstellung.Speichern(db, key);
        var handler = new FakeHandler();
        var fixedTime = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var service = new PulsAuswertungDienst(source, _root, () => { creates++; return new HttpClient(handler); },
            new FixedTime(fixedTime));
        var state = await service.LadenReferenz(CancellationToken.None);
        var updated = await service.AktualisierenReferenz(new(state.Version), CancellationToken.None);
        Assert.Collection(handler.Paths,
            path => Assert.Equal("/personalapitoken-service/personal-api/personal-api-key/token", path),
            path => Assert.Equal("/biometric-service/heartRateZones/v2?generateDefaults=false", path));
        Assert.Equal(fixedTime, updated.Abgerufen);
        Assert.Equal("CYCLING", Assert.Single(updated.Profile).Sport);
        Assert.Collection(updated.Profile[0].Untergrenzen,
            value => Assert.Equal(90, value), value => Assert.Equal(110, value), value => Assert.Equal(130, value),
            value => Assert.Equal(150, value), value => Assert.Equal(170, value));
        Assert.True(handler.Disposed);
        var json = System.Text.Json.JsonSerializer.Serialize(updated);
        Assert.DoesNotContain(key, json);
        Assert.DoesNotContain("FAKE-TOKEN", json);
    }

    [Fact]
    public async Task KeyRotationDuringRefreshCausesCasConflictAndPreservesOldReference()
    {
        const string oldKey = "FAKE-OLD-KEY";
        using (var db = OeffneDatenbank.Ausfuehren(Database))
        {
            GarminEinstellung.Speichern(db, oldKey);
            GarminPulszonenEinstellung.Speichern(db, Reference("RUNNING"));
        }
        var source = new SqlitePulsQuelldaten(Database);
        var handler = new FakeHandler(() =>
        {
            using var db = OeffneDatenbank.Ausfuehren(Database);
            GarminEinstellung.Speichern(db, "FAKE-ROTATED-KEY");
        });
        var service = new PulsAuswertungDienst(source, _root, () => new HttpClient(handler),
            new FixedTime(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        var state = await service.LadenReferenz(CancellationToken.None);
        await Assert.ThrowsAsync<AndyTours.Application.Stammdaten.StammdatenException>(() =>
            service.AktualisierenReferenz(new(state.Version), CancellationToken.None));
        Assert.True(handler.Disposed);
        using var verify = OeffneDatenbank.Ausfuehren(Database);
        Assert.Equal("RUNNING", GarminPulszonenEinstellung.Laden(verify)!.Profile.Single().Sport);
    }

    private static GarminPulszonenStand Reference(string sport) =>
        new(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            [new GarminPulsprofil(sport, [90, 110, 130, 150, 170])]);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeHandler(Action? onProfile = null) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public bool Disposed { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            Paths.Add(path);
            if (path == "/personalapitoken-service/personal-api/personal-api-key/token")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("FAKE-TOKEN", Encoding.UTF8, "text/plain") });
            if (path == "/biometric-service/heartRateZones/v2?generateDefaults=false")
            {
                onProfile?.Invoke();
                const string json = """[{"sport":"CYCLING","zone1Floor":90,"zone2Floor":110,"zone3Floor":130,"zone4Floor":150,"zone5Floor":170}]""";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
            }
            throw new InvalidOperationException("Unexpected fake endpoint");
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }
}
