namespace AndyTours.Tests.Sicherung;

using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Daten.Datensicherung;
using Microsoft.Data.Sqlite;
using Xunit;

public sealed class SicherungsdatenbankSnapshotTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "andy-db-snapshot-tests", Guid.NewGuid().ToString("N"));
    private string Source => Path.Combine(_directory, "source.db");
    private string Stage => Path.Combine(_directory, "stage");
    private static readonly string[] ExpectedReferences = [" 2026/ride.fit "];
    private static SicherungsmanifestGrenzen Limits => new(100, 1024L * 1024 * 1024, 1024L * 1024 * 1024, 200);

    public SicherungsdatenbankSnapshotTests()
    {
        Directory.CreateDirectory(_directory);
        Directory.CreateDirectory(Stage);
        using var connection = OeffneDatenbank.Ausfuehren(Source);
        using var transaction = connection.BeginTransaction();
        var driver = FahrerTabelle.Standardfahrer(connection, transaction);
        var activity = AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "Rad", IstHaupt = true });
        var tour = new Trainingseinheit
        {
            FahrerId = driver, AktivitaetId = activity, Datum = new DateOnly(2026, 1, 1), Name = "seed",
            Fahrzeit = TimeSpan.FromMinutes(1), DistanzKm = 1, Hoehenmeter = 1, MaxHoehe = 1,
            StartHoehe = 1, MaxGeschwindigkeit = 1, HfDurchschnitt = 1, HfMax = 1, HfMin = 1,
            Trittfrequenz = 1, Temperatur = 1, LeistungWatt = 1, KoerpergewichtKg = 1,
            Erholungspuls = 1, Trainingsbereich = "test", Kommentar = "test", KategorieIds = []
        };
        var tourId = TrainingseinheitenTabelle.Einfuegen(connection, transaction, tour).Id;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO Anhang (EinheitId, Typ, RelativerPfad) VALUES ($id, 'GpsTrack', ' 2026/ride.fit '), ($id, 'Hoehenprofil', ' 2026/ride.fit ');";
            command.Parameters.AddWithValue("$id", tourId);
            command.ExecuteNonQuery();
        }
        EinstellungTabelle.Schreiben(connection, transaction, "test.snapshot", "old");
        transaction.Commit();
    }

    [Fact]
    public void SnapshotIsValidPreservesRawDistinctReferencesAndDisposeOnlyDeletesOwnedCopy()
    {
        var sourceBytes = File.ReadAllBytes(Source);
        using var snapshot = SicherungsdatenbankSnapshot.Erstellen(Source, Stage, Limits);
        var snapshotPath = snapshot.SnapshotPfad;
        Assert.Equal(Sicherungspruefung.Brauchbar, PruefeSicherung.Ausfuehren(snapshotPath));
        Assert.Equal(ExpectedReferences, snapshot.ReferenzierteRelativePfade);
        snapshot.PruefeUnveraendert();
        Assert.Equal(sourceBytes, File.ReadAllBytes(Source));
        Assert.True(File.Exists(snapshotPath));
        snapshot.Dispose(); snapshot.Dispose();
        Assert.False(File.Exists(snapshotPath));
        Assert.True(File.Exists(Source));
    }

    [Fact]
    public void ObserverDetectsCommittedChangeFromSeparateConnection()
    {
        using var snapshot = SicherungsdatenbankSnapshot.Erstellen(Source, Stage, Limits);
        using (var connection = OeffneDatenbank.Ausfuehren(Source))
        using (var transaction = connection.BeginTransaction())
        {
            EinstellungTabelle.Schreiben(connection, transaction, "test.snapshot", "new");
            transaction.Commit();
        }
        using (var snapshotConnection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = snapshot.SnapshotPfad, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString()))
        {
            snapshotConnection.Open();
            Assert.Equal("old", EinstellungTabelle.Lesen(snapshotConnection, "test.snapshot"));
        }
        var error = Assert.Throws<StammdatenException>(() => snapshot.PruefeUnveraendert());
        Assert.Equal(StammdatenFehlerart.Konflikt, error.Art);
        Assert.Equal("Datenbestand wurde während der Sicherung geändert.", error.Message);
    }

    [Fact]
    public void ExistingForeignStageContentIsPreservedAndSnapshotDoesNotOverwriteSource()
    {
        var foreign = Path.Combine(Stage, "foreign.txt"); File.WriteAllText(foreign, "preserve");
        var before = File.ReadAllBytes(Source);
        AssertInvalid(() => SicherungsdatenbankSnapshot.Erstellen(Source, Stage, Limits));
        Assert.Equal("preserve", File.ReadAllText(foreign));
        Assert.Equal(before, File.ReadAllBytes(Source));
        File.Delete(foreign);

        File.Copy(Source, Path.Combine(Stage, "andytours.db"));
        var existing = File.ReadAllBytes(Path.Combine(Stage, "andytours.db"));
        AssertInvalid(() => SicherungsdatenbankSnapshot.Erstellen(Source, Stage, Limits));
        Assert.Equal(existing, File.ReadAllBytes(Path.Combine(Stage, "andytours.db")));
    }

    [Fact]
    public void MissingStageIsNotCreatedAndDatabaseSizeLimitFailureCleansOnlyOwnedFile()
    {
        var missing = Path.Combine(_directory, "missing-stage");
        AssertInvalid(() => SicherungsdatenbankSnapshot.Erstellen(Source, missing, Limits));
        Assert.False(Directory.Exists(missing));
        AssertInvalid(() => SicherungsdatenbankSnapshot.Erstellen(Source, Stage, new(100, 1, 1, 200)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Stage));
        Assert.True(File.Exists(Source));
    }

    [Fact]
    public void RejectsCorruptSourceAndPreCancelledCallWithoutChangingSourceOrStaging()
    {
        var corrupt = Path.Combine(_directory, "corrupt.db"); File.WriteAllText(corrupt, "not sqlite");
        var corruptBefore = File.ReadAllBytes(corrupt);
        AssertInvalid(() => SicherungsdatenbankSnapshot.Erstellen(corrupt, Stage, Limits));
        Assert.Equal(corruptBefore, File.ReadAllBytes(corrupt));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Stage));

        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => SicherungsdatenbankSnapshot.Erstellen(Source, Stage, Limits, cts.Token));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Stage));
        AssertInvalid(() => SicherungsdatenbankSnapshot.Erstellen(Source, _directory, Limits));
        Assert.True(File.Exists(Source));
        AssertInvalid(() => SicherungsdatenbankSnapshot.Erstellen(Source, Stage, new(100, 1024L * 1024 * 1024, 1024L * 1024 * 1024, 14)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Stage));
    }

    [Fact]
    public void RejectsTooManyUniqueAttachmentReferences()
    {
        using (var connection = OeffneDatenbank.Ausfuehren(Source))
        using (var transaction = connection.BeginTransaction())
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO Anhang (EinheitId, Typ, RelativerPfad) SELECT EinheitId, 'GpsTrack', 'path-' || Id FROM Anhang LIMIT 1;";
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        AssertInvalid(() => SicherungsdatenbankSnapshot.Erstellen(Source, Stage, new(1, 1024L * 1024 * 1024, 1024L * 1024 * 1024, 200)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Stage));
    }

    [Fact]
    public void RejectsNewerSchemaWithoutChangingSourceOrLeavingSnapshot()
    {
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Source, Mode = SqliteOpenMode.ReadWrite, Pooling = false
        }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = " + (ErstelleSchema.Schemaversion + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ";";
            command.ExecuteNonQuery();
        }
        var before = File.ReadAllBytes(Source);
        AssertInvalid(() => SicherungsdatenbankSnapshot.Erstellen(Source, Stage, Limits));
        Assert.Equal(before, File.ReadAllBytes(Source));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Stage));
    }

    [Fact]
    public void RejectsSourceAndStagingLinksOnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;
        var sourceBefore = File.ReadAllBytes(Source);
        var sourceLink = Path.Combine(_directory, "source-link.db");
        try
        {
            File.CreateSymbolicLink(sourceLink, Source);
            AssertInvalid(() => SicherungsdatenbankSnapshot.Erstellen(sourceLink, Stage, Limits));
            Assert.Empty(Directory.EnumerateFileSystemEntries(Stage));
        }
        finally { if (File.Exists(sourceLink)) File.Delete(sourceLink); }

        Directory.Delete(Stage);
        var realStage = Path.Combine(_directory, "real-stage");
        Directory.CreateDirectory(realStage);
        try
        {
            Directory.CreateSymbolicLink(Stage, realStage);
            AssertInvalid(() => SicherungsdatenbankSnapshot.Erstellen(Source, Stage, Limits));
            Assert.Empty(Directory.EnumerateFileSystemEntries(realStage));
            Assert.Equal(sourceBefore, File.ReadAllBytes(Source));
        }
        finally { if (Directory.Exists(Stage)) Directory.Delete(Stage); }
    }

    private static void AssertInvalid(Action action)
    {
        var error = Assert.Throws<StammdatenException>(action);
        Assert.Equal(StammdatenFehlerart.Ungueltig, error.Art);
        Assert.Equal("Datenbanksicherung konnte nicht vorbereitet werden.", error.Message);
        Assert.DoesNotContain("source.db", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }
}
