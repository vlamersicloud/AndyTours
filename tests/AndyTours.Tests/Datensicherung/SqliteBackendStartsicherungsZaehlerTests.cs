namespace AndyTours.Tests.Datensicherung;

using AndyTours.Application.Sicherung;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Daten.Datensicherung;
using AndyTours.Daten.Datenordner;
using AndyTours.Tests.Datenordner;
using Microsoft.Data.Sqlite;
using Xunit;

public sealed class SqliteBackendStartsicherungsZaehlerTests
{
    [Fact]
    public async Task IncrementsSaturatingCounterAndLeavesPolicyAndFolderRawValuesUntouched()
    {
        using var temp = new TempOrdner();
        TestDatenbestand.SpeichereIn(temp.Pfad, TestDatenbestand.Erzeuge(2));
        const int before = int.MaxValue - 1;
        var customFolder = Path.Combine(temp.Pfad, "CustomBackups");
        SetPolicy(temp.Pfad, new Sicherungseinstellungen
        {
            Sicherungsordner = customFolder,
            Intervall = 7,
            Hoechstzahl = 4,
            StartsSeitSicherung = before
        });
        var rawInterval = ReadSetting(temp.Pfad, Einstellungsschluessel.Sicherungsintervall);
        var rawMax = ReadSetting(temp.Pfad, Einstellungsschluessel.Sicherungshoechstzahl);
        var rawFolder = ReadSetting(temp.Pfad, Einstellungsschluessel.Sicherungsordner);
        var store = new SqliteBackendStartsicherungsZaehler(DatabasePath(temp.Pfad));

        var first = await store.ZaehlenUndErhoehen(CancellationToken.None);
        var second = await store.ZaehlenUndErhoehen(CancellationToken.None);

        Assert.Equal(new BackendStartsicherungsStand(7, int.MaxValue, 2), first);
        Assert.Equal(int.MaxValue, second.StartsSeitSicherung);
        Assert.Equal(rawInterval, ReadSetting(temp.Pfad, Einstellungsschluessel.Sicherungsintervall));
        Assert.Equal(rawMax, ReadSetting(temp.Pfad, Einstellungsschluessel.Sicherungshoechstzahl));
        Assert.Equal(rawFolder, ReadSetting(temp.Pfad, Einstellungsschluessel.Sicherungsordner));
        Assert.Equal(int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ReadSetting(temp.Pfad, Einstellungsschluessel.StartsSeitSicherung));
    }

    [Fact]
    public async Task CounterResetIsCompareAndSwapAndOnlyChangesCounterKey()
    {
        using var temp = new TempOrdner();
        TestDatenbestand.SpeichereIn(temp.Pfad, TestDatenbestand.Erzeuge(1));
        SetPolicy(temp.Pfad, new Sicherungseinstellungen
        {
            Sicherungsordner = Path.Combine(temp.Pfad, "NeverCreated"),
            Intervall = 2,
            Hoechstzahl = 8,
            StartsSeitSicherung = 5
        });
        var interval = ReadSetting(temp.Pfad, Einstellungsschluessel.Sicherungsintervall);
        var max = ReadSetting(temp.Pfad, Einstellungsschluessel.Sicherungshoechstzahl);
        var folder = ReadSetting(temp.Pfad, Einstellungsschluessel.Sicherungsordner);
        var store = new SqliteBackendStartsicherungsZaehler(DatabasePath(temp.Pfad));

        Assert.False(await store.Zuruecksetzen(4, CancellationToken.None));
        Assert.Equal("5", ReadSetting(temp.Pfad, Einstellungsschluessel.StartsSeitSicherung));
        Assert.True(await store.Zuruecksetzen(5, CancellationToken.None));

        Assert.Equal("0", ReadSetting(temp.Pfad, Einstellungsschluessel.StartsSeitSicherung));
        Assert.Equal(interval, ReadSetting(temp.Pfad, Einstellungsschluessel.Sicherungsintervall));
        Assert.Equal(max, ReadSetting(temp.Pfad, Einstellungsschluessel.Sicherungshoechstzahl));
        Assert.Equal(folder, ReadSetting(temp.Pfad, Einstellungsschluessel.Sicherungsordner));
        Assert.False(Directory.Exists(Path.Combine(temp.Pfad, "NeverCreated")));
    }

    [Fact]
    public async Task InvalidIntervalDoesNotChangeAnySetting()
    {
        using var temp = new TempOrdner();
        TestDatenbestand.SpeichereIn(temp.Pfad, TestDatenbestand.Erzeuge(1));
        SetRawSetting(temp.Pfad, Einstellungsschluessel.Sicherungsintervall, "100");
        SetRawSetting(temp.Pfad, Einstellungsschluessel.StartsSeitSicherung, "6");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new SqliteBackendStartsicherungsZaehler(DatabasePath(temp.Pfad)).ZaehlenUndErhoehen(CancellationToken.None));

        Assert.Equal("100", ReadSetting(temp.Pfad, Einstellungsschluessel.Sicherungsintervall));
        Assert.Equal("6", ReadSetting(temp.Pfad, Einstellungsschluessel.StartsSeitSicherung));
    }

    [Fact]
    public async Task MalformedCounterDoesNotChangeAnySetting()
    {
        using var temp = new TempOrdner();
        TestDatenbestand.SpeichereIn(temp.Pfad, TestDatenbestand.Erzeuge(1));
        SetRawSetting(temp.Pfad, Einstellungsschluessel.Sicherungsintervall, "4");
        SetRawSetting(temp.Pfad, Einstellungsschluessel.StartsSeitSicherung, "-1");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new SqliteBackendStartsicherungsZaehler(DatabasePath(temp.Pfad)).ZaehlenUndErhoehen(CancellationToken.None));

        Assert.Equal("4", ReadSetting(temp.Pfad, Einstellungsschluessel.Sicherungsintervall));
        Assert.Equal("-1", ReadSetting(temp.Pfad, Einstellungsschluessel.StartsSeitSicherung));
    }

    [Fact]
    public async Task MissingDatabaseIsNotCreated()
    {
        using var temp = new TempOrdner();
        var path = DatabasePath(temp.Pfad);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            new SqliteBackendStartsicherungsZaehler(path).ZaehlenUndErhoehen(CancellationToken.None));

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task ExistingSchemaWithoutRequiredTablesIsNotMigratedOrWritten()
    {
        using var temp = new TempOrdner();
        var path = DatabasePath(temp.Pfad);
        using (var created = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString()))
        {
            created.Open();
            using var command = created.CreateCommand();
            command.CommandText = "CREATE TABLE KeepOnly (Value TEXT);";
            command.ExecuteNonQuery();
        }
        var before = File.ReadAllBytes(path);

        await Assert.ThrowsAsync<SqliteException>(() =>
            new SqliteBackendStartsicherungsZaehler(path).ZaehlenUndErhoehen(CancellationToken.None));

        Assert.Equal(before, File.ReadAllBytes(path));
        using var verify = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        verify.Open();
        using var verifyCommand = verify.CreateCommand();
        verifyCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Einstellung';";
        Assert.Equal(0L, (long)verifyCommand.ExecuteScalar()!);
    }

    [Fact]
    public async Task PreCancelledResetLeavesCounterUnchanged()
    {
        using var temp = new TempOrdner();
        TestDatenbestand.SpeichereIn(temp.Pfad, TestDatenbestand.Erzeuge(1));
        SetRawSetting(temp.Pfad, Einstellungsschluessel.StartsSeitSicherung, "9");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SqliteBackendStartsicherungsZaehler(DatabasePath(temp.Pfad)).Zuruecksetzen(9, cancellation.Token));

        Assert.Equal("9", ReadSetting(temp.Pfad, Einstellungsschluessel.StartsSeitSicherung));
    }

    [Fact]
    public async Task PreCancelledCallsDoNotOpenOrCreateDatabase()
    {
        using var temp = new TempOrdner();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var path = DatabasePath(temp.Pfad);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SqliteBackendStartsicherungsZaehler(path).ZaehlenUndErhoehen(cancellation.Token));

        Assert.False(File.Exists(path));
    }

    private static string DatabasePath(string directory) => Path.Combine(directory, DatenordnerInhalt.Datenbankdatei);

    private static void SetPolicy(string directory, Sicherungseinstellungen settings)
    {
        using var connection = OeffneDatenbank.Ausfuehren(DatabasePath(directory));
        SicherungEinstellung.Speichern(connection, settings);
    }

    private static void SetRawSetting(string directory, string key, string value)
    {
        using var connection = OeffneDatenbank.Ausfuehren(DatabasePath(directory));
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Einstellung SET Wert = $value WHERE Schluessel = $key;";
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$key", key);
        Assert.Equal(1, command.ExecuteNonQuery());
    }

    private static string? ReadSetting(string directory, string key)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath(directory),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        connection.Open();
        return EinstellungTabelle.Lesen(connection, key);
    }
}
