using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed class SqlitePlanungsProfilTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AndyTours-PlanungsProfil-" + Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(_directory, "profile.db");
    private SqlitePlanungsProfil Service => new(Database);

    public SqlitePlanungsProfilTests()
    {
        Directory.CreateDirectory(_directory);
        using var db = OeffneDatenbank.Ausfuehren(Database);
    }

    [Fact]
    public async Task Empty_read_uses_legacy_default_and_cas_changes_only_profile_key()
    {
        var original = await Service.Laden(default);
        Assert.Equal(CourseFahrprofil.Trekking, original.Profil);
        using (var db = OeffneDatenbank.Ausfuehren(Database))
        {
            Assert.Null(EinstellungTabelle.Lesen(db, Einstellungsschluessel.PlanungProfil));
            EinstellungTabelle.Schreiben(db, null, "Other.Preference", "keep");
        }

        var saved = await Service.Speichern(new(original.Version, CourseFahrprofil.Rennrad), default);
        Assert.Equal(CourseFahrprofil.Rennrad, saved.Profil);
        var conflict = await Assert.ThrowsAsync<StammdatenException>(() =>
            Service.Speichern(new(original.Version, CourseFahrprofil.Mountainbike), default));
        Assert.Equal(StammdatenFehlerart.Konflikt, conflict.Art);
        using var verify = OeffneDatenbank.Ausfuehren(Database);
        Assert.Equal(nameof(Fahrprofil.Rennrad), EinstellungTabelle.Lesen(verify, Einstellungsschluessel.PlanungProfil));
        Assert.Equal("keep", EinstellungTabelle.Lesen(verify, "Other.Preference"));
        var json = JsonSerializer.Serialize(saved);
        Assert.DoesNotContain(Database, json, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenrouteserviceSchluessel", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Legacy_value_changes_version_and_invalid_or_cancelled_writes_do_not_mutate()
    {
        var first = await Service.Laden(default);
        using (var db = OeffneDatenbank.Ausfuehren(Database))
            EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.PlanungProfil, "Mountainbike");
        var changed = await Service.Laden(default);
        Assert.NotEqual(first.Version, changed.Version);
        Assert.Equal(CourseFahrprofil.Mountainbike, changed.Profil);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Service.Speichern(new(changed.Version, CourseFahrprofil.Rennrad), cts.Token));
        var badVersion = await Assert.ThrowsAsync<StammdatenException>(() =>
            Service.Speichern(new("bad", CourseFahrprofil.Rennrad), default));
        Assert.Equal(StammdatenFehlerart.Ungueltig, badVersion.Art);
        var badProfile = await Assert.ThrowsAsync<StammdatenException>(() =>
            Service.Speichern(new(changed.Version, (CourseFahrprofil)999), default));
        Assert.Equal(StammdatenFehlerart.Ungueltig, badProfile.Art);
        var after = await Service.Laden(default);
        Assert.Equal(changed.Version, after.Version);
        Assert.Equal(CourseFahrprofil.Mountainbike, after.Profil);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
