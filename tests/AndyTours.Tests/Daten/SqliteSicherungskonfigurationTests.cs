namespace AndyTours.Tests.Daten;

using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;
using Xunit;

public sealed class SqliteSicherungskonfigurationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "SicherungConfig-" + Guid.NewGuid().ToString("N"));
    private string Db => Path.Combine(_dir, "db.sqlite");
    private SqliteSicherungskonfiguration Service => new(Db);

    public SqliteSicherungskonfigurationTests()
    {
        Directory.CreateDirectory(_dir);
        using var db = OeffneDatenbank.Ausfuehren(Db);
    }

    [Fact]
    public async Task Empty_read_uses_defaults_without_writes()
    {
        var answer = await Service.Laden(default);
        Assert.Equal(3, answer.Intervall);
        Assert.Equal(10, answer.Hoechstzahl);
        Assert.Equal(0, answer.StartsSeitSicherung);
        using var db = OeffneDatenbank.Ausfuehren(Db);
        Assert.Null(EinstellungTabelle.Lesen(db, Einstellungsschluessel.Sicherungsintervall));
        Assert.Null(EinstellungTabelle.Lesen(db, Einstellungsschluessel.Sicherungshoechstzahl));
        Assert.Null(EinstellungTabelle.Lesen(db, Einstellungsschluessel.Sicherungsordner));
        Assert.Null(EinstellungTabelle.Lesen(db, Einstellungsschluessel.StartsSeitSicherung));
    }

    [Fact]
    public async Task Save_changes_only_policy_keys_and_preserves_folder_counter_and_other_values()
    {
        using (var db = OeffneDatenbank.Ausfuehren(Db))
        {
            EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.Sicherungsordner, "custom-folder-value");
            EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.StartsSeitSicherung, "17");
            EinstellungTabelle.Schreiben(db, null, "Other.keep", "preserved");
        }

        var before = await Service.Laden(default);
        var saved = await Service.Speichern(new(before.Version, 1, 99), default);
        Assert.Equal(1, saved.Intervall);
        Assert.Equal(99, saved.Hoechstzahl);
        Assert.Equal(17, saved.StartsSeitSicherung);
        using var verify = OeffneDatenbank.Ausfuehren(Db);
        Assert.Equal("1", EinstellungTabelle.Lesen(verify, Einstellungsschluessel.Sicherungsintervall));
        Assert.Equal("99", EinstellungTabelle.Lesen(verify, Einstellungsschluessel.Sicherungshoechstzahl));
        Assert.Equal("custom-folder-value", EinstellungTabelle.Lesen(verify, Einstellungsschluessel.Sicherungsordner));
        Assert.Equal("17", EinstellungTabelle.Lesen(verify, Einstellungsschluessel.StartsSeitSicherung));
        Assert.Equal("preserved", EinstellungTabelle.Lesen(verify, "Other.keep"));
    }

    [Fact]
    public async Task Counter_and_folder_changes_do_not_change_policy_version_or_invalidate_cas()
    {
        var before = await Service.Laden(default);
        using (var db = OeffneDatenbank.Ausfuehren(Db))
        {
            EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.StartsSeitSicherung, "23");
            EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.Sicherungsordner, "another-folder");
        }
        var afterIndependentChange = await Service.Laden(default);
        Assert.Equal(before.Version, afterIndependentChange.Version);
        Assert.Equal(23, afterIndependentChange.StartsSeitSicherung);
        var saved = await Service.Speichern(new(before.Version, 4, 8), default);
        Assert.Equal(4, saved.Intervall);
        Assert.Equal(8, saved.Hoechstzahl);
        Assert.Equal(23, saved.StartsSeitSicherung);
        using var verify = OeffneDatenbank.Ausfuehren(Db);
        Assert.Equal("another-folder", EinstellungTabelle.Lesen(verify, Einstellungsschluessel.Sicherungsordner));
    }

    [Fact]
    public async Task Stale_policy_version_conflicts_without_mutating_settings()
    {
        var initial = await Service.Laden(default);
        var changed = await Service.Speichern(new(initial.Version, 5, 6), default);
        var before = Snapshot();
        var ex = await Assert.ThrowsAsync<StammdatenException>(() =>
            Service.Speichern(new(initial.Version, 7, 8), default));
        Assert.Equal(StammdatenFehlerart.Konflikt, ex.Art);
        Assert.Equal(before, Snapshot());
        Assert.Equal(5, changed.Intervall);
    }

    [Fact]
    public async Task Raw_invalid_values_use_defaults_but_raw_key_changes_advance_version()
    {
        using (var db = OeffneDatenbank.Ausfuehren(Db))
        {
            EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.Sicherungsintervall, "bad-interval");
            EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.Sicherungshoechstzahl, "0");
        }
        var invalid = await Service.Laden(default);
        Assert.Equal(3, invalid.Intervall);
        Assert.Equal(10, invalid.Hoechstzahl);
        using (var db = OeffneDatenbank.Ausfuehren(Db))
            EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.Sicherungsintervall, "bad-interval-two");
        var changedRaw = await Service.Laden(default);
        Assert.NotEqual(invalid.Version, changedRaw.Version);
        Assert.Equal(3, changedRaw.Intervall);
        Assert.Equal(10, changedRaw.Hoechstzahl);
    }

    [Fact]
    public async Task Invalid_and_cancelled_requests_do_not_mutate()
    {
        var initial = await Service.Laden(default);
        var before = Snapshot();
        var invalids = new SicherungskonfigurationAnfrage?[]
        {
            null,
            new(null!, 3, 10),
            new("not-a-version", 3, 10),
            new(new string('g', 64), 3, 10),
            new(new string('A', 64), 0, 10),
            new(new string('A', 64), 3, 100)
        };
        foreach (var request in invalids)
        {
            var ex = await Assert.ThrowsAsync<StammdatenException>(() => Service.Speichern(request!, default));
            Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Service.Speichern(new(initial.Version, 4, 5), cts.Token));
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public async Task Policy_version_is_case_insensitive_hex_and_rollback_keeps_both_keys()
    {
        var initial = await Service.Laden(default);
        var lower = initial.Version.ToLowerInvariant();
        var saved = await Service.Speichern(new(lower, 6, 7), default);
        Assert.Equal(6, saved.Intervall);
        Assert.Equal(7, saved.Hoechstzahl);

        using (var db = OeffneDatenbank.Ausfuehren(Db))
        using (var command = db.CreateCommand())
        {
            command.CommandText = $"""
                CREATE TRIGGER reject_policy_max BEFORE INSERT ON Einstellung
                WHEN NEW.Schluessel = '{Einstellungsschluessel.Sicherungshoechstzahl}'
                BEGIN SELECT RAISE(ABORT, 'test'); END;
                """;
            command.ExecuteNonQuery();
        }
        var before = Snapshot();
        await Assert.ThrowsAsync<SqliteException>(() =>
            Service.Speichern(new(saved.Version, 11, 12), default));
        Assert.Equal(before, Snapshot());
    }

    private string Snapshot()
    {
        using var db = OeffneDatenbank.Ausfuehren(Db);
        return string.Join("|",
            EinstellungTabelle.Lesen(db, Einstellungsschluessel.Sicherungsintervall) ?? "<null>",
            EinstellungTabelle.Lesen(db, Einstellungsschluessel.Sicherungshoechstzahl) ?? "<null>",
            EinstellungTabelle.Lesen(db, Einstellungsschluessel.Sicherungsordner) ?? "<null>",
            EinstellungTabelle.Lesen(db, Einstellungsschluessel.StartsSeitSicherung) ?? "<null>",
            EinstellungTabelle.Lesen(db, "Other.keep") ?? "<null>");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}
