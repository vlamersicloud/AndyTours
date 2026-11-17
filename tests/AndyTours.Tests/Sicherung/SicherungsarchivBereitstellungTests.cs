namespace AndyTours.Tests.Sicherung;

using AndyTours.Api;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Daten.Datensicherung;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class SicherungsarchivBereitstellungTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "andy-provider-tests", Guid.NewGuid().ToString("N"));
    private string Root => Path.Combine(_base, "data");
    private string Db => Path.Combine(Root, "andytours.db");
    private string DefaultBackup => Path.Combine(Root, Sicherungseinstellungen.StandardUnterordner);
    private readonly IConfigurationRoot _configuration = BuildConfiguration(valid: true);

    public SicherungsarchivBereitstellungTests() => Directory.CreateDirectory(Root);

    [Fact]
    public void MissingLimitsReturnsUnavailableWithoutCreatingRootOrDatabase()
    {
        var config = BuildConfiguration(valid: false);
        using var configOwner = (IDisposable)config;
        var missingRoot = Path.Combine(_base, "not-created");
        Assert.Null(new SicherungsarchivBereitstellung(config, missingRoot, TimeProvider.System).Ermittle(CancellationToken.None));
        Assert.False(Directory.Exists(missingRoot));
    }

    [Fact]
    public void ResolvesDefaultFolderReadOnlyWithoutCreatingItOrChangingDatabase()
    {
        CreateDatabase();
        var before = File.ReadAllBytes(Db);
        var result = new SicherungsarchivBereitstellung(_configuration, Root, TimeProvider.System).Ermittle(CancellationToken.None);
        Assert.NotNull(result);
        Assert.False(Directory.Exists(DefaultBackup));
        Assert.Equal(before, File.ReadAllBytes(Db));
    }

    [Fact]
    public void PreservesCustomAbsoluteFolderAndDoesNotCreateIt()
    {
        CreateDatabase();
        var custom = Path.Combine(_base, "custom-backups");
        WriteSetting(Einstellungsschluessel.Sicherungsordner, custom);
        WriteSetting(Einstellungsschluessel.StartsSeitSicherung, "7");
        var before = File.ReadAllBytes(Db);
        var result = new SicherungsarchivBereitstellung(_configuration, Root, TimeProvider.System).Ermittle(CancellationToken.None);
        Assert.NotNull(result);
        Assert.False(Directory.Exists(custom));
        Assert.Equal(before, File.ReadAllBytes(Db));
        Assert.Equal(custom, ReadSetting(Einstellungsschluessel.Sicherungsordner));
        Assert.Equal("7", ReadSetting(Einstellungsschluessel.StartsSeitSicherung));
    }

    [Fact]
    public void RejectsRelativeEqualRootAndFileBackupSettings()
    {
        CreateDatabase();
        WriteSetting(Einstellungsschluessel.Sicherungsordner, "relative");
        Assert.Null(Resolve());
        WriteSetting(Einstellungsschluessel.Sicherungsordner, Root);
        Assert.Null(Resolve());
        var file = Path.Combine(_base, "not-a-directory");
        File.WriteAllText(file, "x");
        WriteSetting(Einstellungsschluessel.Sicherungsordner, file);
        Assert.Null(Resolve());
    }

    [Fact]
    public void MissingAndCorruptDatabaseReturnUnavailableWithoutMigrationOrWrites()
    {
        Assert.Null(Resolve());
        File.WriteAllBytes(Db, [1, 2, 3, 4]);
        var before = File.ReadAllBytes(Db);
        Assert.Null(Resolve());
        Assert.Equal(before, File.ReadAllBytes(Db));
        Assert.False(Directory.Exists(DefaultBackup));
    }

    [Fact]
    public void PreCancelledRequestDoesNotTouchFilesystem()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            new SicherungsarchivBereitstellung(_configuration, Root, TimeProvider.System).Ermittle(cts.Token));
        Assert.False(File.Exists(Db));
    }

    [Fact]
    public void RejectsSymlinkedDatabaseAndRootOrBackupAncestorsOnUnix()
    {
        if (OperatingSystem.IsWindows()) return;
        CreateDatabase();
        var before = File.ReadAllBytes(Db);
        var rootAlias = Path.Combine(_base, "root-alias");
        Directory.CreateSymbolicLink(rootAlias, Root);
        Assert.Null(new SicherungsarchivBereitstellung(_configuration, rootAlias, TimeProvider.System).Ermittle(CancellationToken.None));
        Assert.Equal(before, File.ReadAllBytes(Db));

        var realBackup = Path.Combine(_base, "real-backup");
        Directory.CreateDirectory(realBackup);
        var backupAlias = Path.Combine(_base, "backup-alias");
        Directory.CreateSymbolicLink(backupAlias, realBackup);
        WriteSetting(Einstellungsschluessel.Sicherungsordner, Path.Combine(backupAlias, "archive"));
        var configuredBefore = File.ReadAllBytes(Db);
        Assert.Null(Resolve());
        Assert.Empty(Directory.GetFileSystemEntries(realBackup));
        Assert.False(Directory.Exists(Path.Combine(backupAlias, "archive")));
        Assert.Equal(configuredBefore, File.ReadAllBytes(Db));
    }

    [Fact]
    public void RejectsSymlinkedDatabaseOnUnix()
    {
        if (OperatingSystem.IsWindows()) return;
        CreateDatabase();
        var real = Path.Combine(_base, "real.db");
        File.Move(Db, real);
        File.CreateSymbolicLink(Db, real);
        Assert.Null(Resolve());
    }

    private DateisystemSicherungsarchivErstellung? Resolve() =>
        new SicherungsarchivBereitstellung(_configuration, Root, TimeProvider.System).Ermittle(CancellationToken.None);

    private void CreateDatabase()
    {
        using var connection = OeffneDatenbank.Ausfuehren(Db);
    }

    private void WriteSetting(string key, string value)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Db, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        connection.Open();
        using var tx = connection.BeginTransaction();
        EinstellungTabelle.Schreiben(connection, tx, key, value);
        tx.Commit();
    }

    private string? ReadSetting(string key)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Db, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Wert FROM Einstellung WHERE Schluessel = $key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    private static IConfigurationRoot BuildConfiguration(bool valid)
    {
        var values = valid ? new Dictionary<string, string?>
        {
            ["Backend:Sicherung:MaxEintraege"] = "10",
            ["Backend:Sicherung:MaxDateiBytes"] = "1000",
            ["Backend:Sicherung:MaxGesamtBytes"] = "2000",
            ["Backend:Sicherung:MaxPfadzeichen"] = "100",
            ["Backend:Sicherung:MaxArchivBytes"] = "10000",
            ["Backend:Sicherung:MaxManifestBytes"] = "1000",
            ["Backend:Sicherung:MaxZentralverzeichnisBytes"] = "2000"
        } : new Dictionary<string, string?>();
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    public void Dispose()
    {
        ((IDisposable)_configuration).Dispose();
        if (Directory.Exists(_base)) Directory.Delete(_base, recursive: true);
        GC.SuppressFinalize(this);
    }
}
