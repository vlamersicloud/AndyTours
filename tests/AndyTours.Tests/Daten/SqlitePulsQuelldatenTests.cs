using System.Text.Json;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Stammdaten;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed class SqlitePulsQuelldatenTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AndyTours-PulseSource-" + Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(_directory, "test.db");
    private SqlitePulsQuelldaten Source => new(Database);

    public SqlitePulsQuelldatenTests()
    {
        Directory.CreateDirectory(_directory);
        using var db = OeffneDatenbank.Ausfuehren(Database);
    }

    [Fact]
    public async Task EmptyReadIsWriteFreeAndReferenceSaveKeepsOtherSettingsAndSecretsPrivate()
    {
        var empty = await Source.ReferenzLaden(CancellationToken.None);
        Assert.Null(empty.Schluessel);
        Assert.Null(empty.Stand);
        Assert.Matches("^[0-9A-F]{64}$", empty.Version);
        using (var db = OeffneDatenbank.Ausfuehren(Database))
        {
            Assert.Null(EinstellungTabelle.Lesen(db, Einstellungsschluessel.GarminPersonalApiKey));
            Assert.Null(EinstellungTabelle.Lesen(db, "Persoenlich.Garmin.PulszonenReferenz"));
            using var count = db.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM Einstellung;";
            Assert.Equal(0L, count.ExecuteScalar());
        }

        const string fakeKey = "FakePulseKey_Only+456";
        var stand = Reference();
        using (var db = OeffneDatenbank.Ausfuehren(Database))
        {
            GarminEinstellung.Speichern(db, fakeKey);
            EinstellungTabelle.Schreiben(db, null, "Unrelated.Preference", "keep-me");
        }
        var withKey = await Source.ReferenzLaden(CancellationToken.None);
        var saved = await Source.ReferenzSpeichern(withKey.Version, stand, CancellationToken.None);
        Assert.Equal(stand.Abgerufen, saved.Stand!.Abgerufen);
        Assert.Equal(stand.Profile.Select(p => p.Sport), saved.Stand.Profile.Select(p => p.Sport));
        Assert.Equal(stand.Profile[0].Untergrenzen, saved.Stand.Profile[0].Untergrenzen);
        Assert.Equal(fakeKey, saved.Schluessel);
        Assert.DoesNotContain(fakeKey, saved.ToString());
        var json = JsonSerializer.Serialize(saved);
        Assert.DoesNotContain(fakeKey, json);
        Assert.DoesNotContain("Schluessel", json);

        using var verify = OeffneDatenbank.Ausfuehren(Database);
        Assert.Equal(fakeKey, GarminEinstellung.Laden(verify));
        Assert.Equal("keep-me", EinstellungTabelle.Lesen(verify, "Unrelated.Preference"));
        Assert.Equal(JsonSerializer.Serialize(stand), EinstellungTabelle.Lesen(verify, "Persoenlich.Garmin.PulszonenReferenz"));
    }

    [Fact]
    public async Task StaleVersionsAndInvalidReferencesCannotOverwrite()
    {
        const string fakeOld = "FakeOldKey";
        const string fakeNew = "FakeNewKey";
        using (var db = OeffneDatenbank.Ausfuehren(Database))
        {
            GarminEinstellung.Speichern(db, fakeOld);
            GarminPulszonenEinstellung.Speichern(db, Reference());
        }
        var initial = await Source.ReferenzLaden(CancellationToken.None);
        using (var db = OeffneDatenbank.Ausfuehren(Database))
            GarminEinstellung.Speichern(db, fakeNew);
        var conflict = await Assert.ThrowsAsync<StammdatenException>(() =>
            Source.ReferenzSpeichern(initial.Version, Reference("RUNNING"), CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.Konflikt, conflict.Art);
        Assert.DoesNotContain("Fake", conflict.Message);

        var current = await Source.ReferenzLaden(CancellationToken.None);
        var invalid = new[]
        {
            Reference(boundaries: [90, 85, 120, 150, 180]),
            Reference(sports: ["CYCLING", "CYCLING"]),
            Reference(sports: [])
        };
        foreach (var bad in invalid)
        {
            var error = await Assert.ThrowsAsync<StammdatenException>(() =>
                Source.ReferenzSpeichern(current.Version, bad, CancellationToken.None));
            Assert.Equal(StammdatenFehlerart.Ungueltig, error.Art);
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Source.ReferenzSpeichern(current.Version, Reference("RUNNING"), cts.Token));
        using var verify = OeffneDatenbank.Ausfuehren(Database);
        Assert.Equal(fakeNew, GarminEinstellung.Laden(verify));
        Assert.Equal("CYCLING", GarminPulszonenEinstellung.Laden(verify)!.Profile.Single().Sport);
    }

    private static GarminPulszonenStand Reference(string sport = "CYCLING", int[]? boundaries = null, string[]? sports = null) =>
        new(new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero),
            (sports ?? [sport]).Select(name => new GarminPulsprofil(name, boundaries ?? [90, 110, 130, 150, 170])).ToArray());

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
