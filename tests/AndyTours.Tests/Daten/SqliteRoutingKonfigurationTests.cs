using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed class SqliteRoutingKonfigurationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RouteConfig-" + Guid.NewGuid().ToString("N"));
    private string Db => Path.Combine(_dir, "db.sqlite");
    private SqliteRoutingKonfiguration Service => new(Db);
    private const string Secret = "FAKE-ORS-KEY";
    private const string UnsafeUrl = "https://fake-user:fake-pass@router.invalid/path?token=FAKE#fragment";

    public SqliteRoutingKonfigurationTests()
    {
        Directory.CreateDirectory(_dir);
        using var db = OeffneDatenbank.Ausfuehren(Db);
    }

    [Fact]
    public async Task Empty_read_uses_legacy_defaults_without_writes()
    {
        var answer = await Service.Laden(default);
        Assert.Equal(RoutingAnbieter.BRouter, answer.Dienst);
        Assert.Equal(Routingeinstellungen.StandardBRouterAdresse, answer.BRouterAdresse);
        Assert.False(answer.AdresseAusgeblendet);
        Assert.False(answer.HatOrsSchluessel);
        using var db = OeffneDatenbank.Ausfuehren(Db);
        Assert.Null(EinstellungTabelle.Lesen(db, Einstellungsschluessel.RoutingDienst));
        Assert.Null(EinstellungTabelle.Lesen(db, Einstellungsschluessel.RoutingBRouterAdresse));
        Assert.Null(EinstellungTabelle.Lesen(db, Einstellungsschluessel.RoutingOpenrouteserviceSchluessel));
    }

    [Fact]
    public async Task CAS_writes_only_one_key_hides_raw_credentials_and_preserves_other_settings()
    {
        using (var db = OeffneDatenbank.Ausfuehren(Db))
        {
            EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.RoutingBRouterAdresse, UnsafeUrl);
            EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.RoutingOpenrouteserviceSchluessel, Secret);
            EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.PlanungProfil, "Rennrad");
            EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.PlanungOrdner, "fake-folder");
            EinstellungTabelle.Schreiben(db, null, "Other.fake", "keep");
        }
        var first = await Service.Laden(default);
        Assert.Null(first.BRouterAdresse);
        Assert.True(first.AdresseAusgeblendet);
        Assert.True(first.HatOrsSchluessel);
        var serialized = JsonSerializer.Serialize(first);
        Assert.DoesNotContain("fake-user", serialized); Assert.DoesNotContain("fake-pass", serialized);
        Assert.DoesNotContain("FAKE", serialized); Assert.DoesNotContain("token", serialized);

        var changed = await Service.DienstSpeichern(new(first.Version, RoutingAnbieter.Openrouteservice), default);
        Assert.Equal(RoutingAnbieter.Openrouteservice, changed.Dienst);
        await Assert.ThrowsAsync<StammdatenException>(() => Service.DienstSpeichern(new(first.Version, RoutingAnbieter.BRouter), default));
        var replaced = await Service.AdresseSpeichern(new(changed.Version, " https://router.example/custom "), default);
        Assert.Equal("https://router.example/custom", replaced.BRouterAdresse);
        var reset = await Service.AdresseZuruecksetzen(new(replaced.Version), default);
        Assert.Equal(Routingeinstellungen.StandardBRouterAdresse, reset.BRouterAdresse);
        var removed = await Service.OrsKeyEntfernen(new(reset.Version), default);
        Assert.False(removed.HatOrsSchluessel);
        using var verify = OeffneDatenbank.Ausfuehren(Db);
        Assert.Null(EinstellungTabelle.Lesen(verify, Einstellungsschluessel.RoutingBRouterAdresse));
        Assert.Null(EinstellungTabelle.Lesen(verify, Einstellungsschluessel.RoutingOpenrouteserviceSchluessel));
        Assert.Equal("Openrouteservice", EinstellungTabelle.Lesen(verify, Einstellungsschluessel.RoutingDienst));
        Assert.Equal("Rennrad", EinstellungTabelle.Lesen(verify, Einstellungsschluessel.PlanungProfil));
        Assert.Equal("fake-folder", EinstellungTabelle.Lesen(verify, Einstellungsschluessel.PlanungOrdner));
        Assert.Equal("keep", EinstellungTabelle.Lesen(verify, "Other.fake"));
    }

    [Fact]
    public async Task Key_rotation_changes_CAS_version_and_invalid_or_cancelled_writes_do_not_mutate()
    {
        var start = await Service.Laden(default);
        var saved = await Service.OrsKeySpeichern(new(start.Version, Secret), default);
        Assert.NotEqual(start.Version, saved.Version);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.OrsKeyEntfernen(new(saved.Version), cts.Token));
        await Assert.ThrowsAsync<StammdatenException>(() => Service.OrsKeySpeichern(new(saved.Version, "\ud800"), default));
        var after = await Service.Laden(default);
        Assert.Equal(saved.Version, after.Version); Assert.True(after.HatOrsSchluessel);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}
