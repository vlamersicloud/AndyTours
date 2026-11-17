using AndyTours.Application.Stammdaten;
using AndyTours.Application.Tabellenlayout;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Tabellenlayout;

public sealed class SqliteEinheitentabellenlayoutTests
{
    private static string NewPath() => Path.Combine(Path.GetTempPath(), $"layout-{Guid.NewGuid():N}.db");
    private static void Seed(string path, string? order, string? hidden)
    {
        using var db = OeffneDatenbank.Ausfuehren(path);
        EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.Spaltenanordnung, order);
        EinstellungTabelle.Schreiben(db, null, Einstellungsschluessel.AusgeblendeteSpalten, hidden);
    }
    private static (bool Exists, string? Value) Raw(SqliteConnection db, string key)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT Wert FROM Einstellung WHERE Schluessel=$k";
        cmd.Parameters.AddWithValue("$k", key);
        using var r = cmd.ExecuteReader();
        return !r.Read() ? (false, null) : (true, r.IsDBNull(0) ? null : r.GetString(0));
    }

    [Fact]
    public async Task GetDoesNotChangeRawValuesAndMissingDiffersFromSqlNull()
    {
        var path = NewPath();
        try
        {
            Seed(path, "Datum:96,Name:250", "Kommentar");
            var store = new SqliteEinheitentabellenlayout(path);
            (bool, string?) beforeOrder, beforeHidden;
            using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
            { db.Open(); beforeOrder = Raw(db, Einstellungsschluessel.Spaltenanordnung); beforeHidden = Raw(db, Einstellungsschluessel.AusgeblendeteSpalten); }
            var before = await store.Laden(default);
            using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
            { db.Open(); Assert.Equal(beforeOrder, Raw(db, Einstellungsschluessel.Spaltenanordnung)); Assert.Equal(beforeHidden, Raw(db, Einstellungsschluessel.AusgeblendeteSpalten)); }
            using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
            { db.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "DELETE FROM Einstellung WHERE Schluessel=$k"; cmd.Parameters.AddWithValue("$k", Einstellungsschluessel.AusgeblendeteSpalten); cmd.ExecuteNonQuery(); }
            var missing = await store.Laden(default);
            Assert.NotEqual(before.Version, missing.Version);

            var nullPath = NewPath();
            try
            {
                using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = nullPath }.ToString()))
                {
                    db.Open();
                    using var cmd = db.CreateCommand();
                    cmd.CommandText = "CREATE TABLE Einstellung (Schluessel TEXT PRIMARY KEY, Wert TEXT NULL); INSERT INTO Einstellung VALUES ($k, NULL);";
                    cmd.Parameters.AddWithValue("$k", Einstellungsschluessel.AusgeblendeteSpalten);
                    cmd.ExecuteNonQuery();
                }
                using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = nullPath }.ToString()))
                {
                    db.Open();
                    using var cmd = db.CreateCommand();
                    cmd.CommandText = "CREATE TABLE IF NOT EXISTS Einstellung (Schluessel TEXT PRIMARY KEY, Wert TEXT NULL); INSERT OR IGNORE INTO Einstellung VALUES ($k, $v);";
                    cmd.Parameters.AddWithValue("$k", Einstellungsschluessel.Spaltenanordnung);
                    cmd.Parameters.AddWithValue("$v", "Datum:96,Name:250");
                    cmd.ExecuteNonQuery();
                }
                var nullVersion = await new SqliteEinheitentabellenlayout(nullPath).Laden(default);
                Assert.NotEqual(missing.Version, nullVersion.Version);
            }
            finally { try { File.Delete(nullPath); } catch (IOException) { } }
        }
        finally { try { File.Delete(path); } catch (IOException) { } }
    }

    [Fact]
    public async Task PutPersistsBothLegacyKeysAndExternalChangeToEitherKeyConflicts()
    {
        foreach (var changedKey in new[] { Einstellungsschluessel.Spaltenanordnung, Einstellungsschluessel.AusgeblendeteSpalten })
        {
            var path = NewPath();
            try
            {
                Seed(path, "Datum:96,Name:250", "Kommentar");
                var store = new SqliteEinheitentabellenlayout(path);
                var original = await store.Laden(default);
                var request = new EinheitentabellenlayoutAnfrage(original.Version, Array.AsReadOnly(original.Anordnung.Select(x => x with { Breite = x.Schluessel == Einheitentabellenschluessel.Datum ? 130.5 : null }).ToArray()), original.Ausgeblendet);
                var written = await store.Speichern(request, default);
                using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
                {
                    db.Open();
                    var order = EinstellungTabelle.Lesen(db, Einstellungsschluessel.Spaltenanordnung);
                    var hidden = EinstellungTabelle.Lesen(db, Einstellungsschluessel.AusgeblendeteSpalten);
                    Assert.Contains("Datum:130.5", order);
                    Assert.Equal("Kommentar", hidden);
                    EinstellungTabelle.Schreiben(db, null, changedKey, changedKey == Einstellungsschluessel.Spaltenanordnung ? "Name:270" : "Datum");
                }
                var stale = await Assert.ThrowsAsync<StammdatenException>(() => store.Speichern(request with { Version = written.Version }, default));
                Assert.Equal(StammdatenFehlerart.Konflikt, stale.Art);
            }
            finally { try { File.Delete(path); } catch (IOException) { } }
        }
    }

    [Fact]
    public async Task SecondWriteFailureRollsBackFirstWriteAndPreCancelledPutDoesNotWrite()
    {
        var path = NewPath();
        try
        {
            Seed(path, "Datum:96,Name:250", "Kommentar");
            var store = new SqliteEinheitentabellenlayout(path);
            var baseline = await store.Laden(default);
            using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
            {
                db.Open();
                var beforeOrder = Raw(db, Einstellungsschluessel.Spaltenanordnung);
                var beforeHidden = Raw(db, Einstellungsschluessel.AusgeblendeteSpalten);
                using (var trigger = db.CreateCommand())
                {
                    trigger.CommandText = "CREATE TRIGGER fail_hidden BEFORE INSERT ON Einstellung WHEN NEW.Schluessel='Einheitentabelle.AusgeblendeteSpalten' BEGIN SELECT RAISE(ABORT, 'fixture'); END;";
                    trigger.ExecuteNonQuery();
                }
                var request = new EinheitentabellenlayoutAnfrage(baseline.Version, baseline.Anordnung, baseline.Ausgeblendet);
                var error = await Assert.ThrowsAsync<EinheitentabellenlayoutNichtVerfuegbarException>(() => store.Speichern(request, default));
                Assert.NotNull(error);
                Assert.Equal(beforeOrder, Raw(db, Einstellungsschluessel.Spaltenanordnung));
                Assert.Equal(beforeHidden, Raw(db, Einstellungsschluessel.AusgeblendeteSpalten));
            }
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Speichern(new EinheitentabellenlayoutAnfrage(baseline.Version, baseline.Anordnung, baseline.Ausgeblendet), cancelled.Token));
            Assert.Equal(baseline.Version, (await store.Laden(default)).Version);
        }
        finally { try { File.Delete(path); } catch (IOException) { } }
    }
}
