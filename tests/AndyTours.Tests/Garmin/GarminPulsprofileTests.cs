using System.Net;
using System.Text.Json;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Import.Garmin;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Garmin;

/// <summary>Vertrag mit offiziellem HeartRateZonesDTO; kein Netzwerk und keine echten Zugangsdaten.</summary>
public sealed class GarminPulsprofileTests
{
    private static readonly int[] ErwarteteGrenzen = [90, 110, 130, 150, 170];
    private const string Beispiel = """
        [{"sport":"CYCLING","zone1Floor":90,"zone2Floor":110,"zone3Floor":130,"zone4Floor":150,"zone5Floor":170}]
        """;

    [Fact]
    public async Task AktuellerReferenzabrufVerwendetOffiziellenEndpunktOhneGenerierteDefaults()
    {
        using var handler = new Antworten();
        using var http = new HttpClient(handler);
        var profile = await new GarminPersonalClient(http, "test-key").LesePulsprofileAsync();
        Assert.Single(profile);
        Assert.Equal("CYCLING", profile[0].Sport);
        Assert.Equal(2, profile[0].Zone(110));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[{}]")]
    [InlineData("[{\"sport\":\"CYCLING\",\"zone1Floor\":90}]")]
    [InlineData("[{\"sport\":\"CYCLING\",\"zone1Floor\":90,\"zone2Floor\":80,\"zone3Floor\":130,\"zone4Floor\":150,\"zone5Floor\":170}]")]
    public void UnvollstaendigeGrenzenWerdenNichtGeraten(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        Assert.Throws<InvalidDataException>(() => LeseGarminPulsprofile.Ausfuehren(doc.RootElement));
    }

    [Fact]
    public void ReferenzcacheBehaeltAbrufdatumUndSportprofil()
    {
        using var db = new SqliteConnection("Data Source=:memory:");
        db.Open();
        using (SqliteCommand cmd = db.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE Einstellung (Schluessel TEXT PRIMARY KEY, Wert TEXT NOT NULL);";
            cmd.ExecuteNonQuery();
        }
        using JsonDocument doc = JsonDocument.Parse(Beispiel);
        var zeit = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        GarminPulszonenEinstellung.Speichern(db, new GarminPulszonenStand(zeit, LeseGarminPulsprofile.Ausfuehren(doc.RootElement)));
        var cache = GarminPulszonenEinstellung.Laden(db);
        Assert.NotNull(cache);
        Assert.Equal(zeit, cache.Abgerufen);
        Assert.Equal(ErwarteteGrenzen, cache.Profile[0].Untergrenzen);
    }

    private sealed class Antworten : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("personal.connectapi.garmin.com", request.RequestUri!.Host);
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal("ApiKey test-key", request.Headers.Authorization!.ToString());
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("test-token") });
            }
            Assert.Equal("/biometric-service/heartRateZones/v2?generateDefaults=false", request.RequestUri.PathAndQuery);
            Assert.Equal("Bearer test-token", request.Headers.Authorization!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Beispiel) });
        }
    }
}
