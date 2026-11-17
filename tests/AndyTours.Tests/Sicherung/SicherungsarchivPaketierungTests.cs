namespace AndyTours.Tests.Sicherung;

using System.IO.Compression;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Core.Modell;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using AndyTours.Daten.Datensicherung;
using Microsoft.Data.Sqlite;
using Xunit;

public sealed class SicherungsarchivPaketierungTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "andy-packer-tests", Guid.NewGuid().ToString("N"), "data");
    private string Db => Path.Combine(_root, "andytours.db");
    private string Stage => Path.Combine(_root, "Sicherungen", "stage");
    private static readonly SicherungsmanifestGrenzen Limits = new(100, 1024L * 1024 * 1024, 1024L * 1024 * 1024, 200);
    private static readonly IReadOnlyList<string> Excluded = ["Sicherungen"];
    private static readonly DateTimeOffset Created = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] ExpectedZipEntries = ["andytours.db", "2026/ride.fit", "orphan.bin", "manifest.json"];

    public SicherungsarchivPaketierungTests()
    {
        Directory.CreateDirectory(Stage);
        Directory.CreateDirectory(Path.Combine(_root, "2026"));
        File.WriteAllBytes(Path.Combine(_root, "2026", "ride.fit"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(_root, "orphan.bin"), [4]);
        using var connection = OeffneDatenbank.Ausfuehren(Db);
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
        var id = TrainingseinheitenTabelle.Einfuegen(connection, transaction, tour).Id;
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "INSERT INTO Anhang (EinheitId, Typ, RelativerPfad) VALUES ($id, 'GpsTrack', '2026/ride.fit');";
        command.Parameters.AddWithValue("$id", id); command.ExecuteNonQuery(); transaction.Commit();
    }

    [Fact]
    public async Task PacksValidSnapshotAndFilesIntoCallerOwnedStream()
    {
        var sourceBefore = Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Where(x => !x.StartsWith(Stage, StringComparison.Ordinal)).ToDictionary(x => x, File.ReadAllBytes);
        using var snapshot = SicherungsdatenbankSnapshot.Erstellen(Db, Stage, Limits);
        var files = SicherungsquellenInventar.Ermittle(_root, Path.Combine(_root, "Sicherungen"), snapshot.ReferenzierteRelativePfade,
            new FileInfo(snapshot.SnapshotPfad).Length, Limits);
        using var target = new MemoryStream();
        var manifest = await SicherungsarchivPaketierung.Erstellen(target, _root, snapshot, files, Created, Limits, Excluded, 20_000_000, 65_536);
        Assert.Equal(1, manifest.Formatversion);
        Assert.Equal(3, manifest.Eintraege.Count);
        Assert.Equal(Created, manifest.ErstelltUtc);
        Assert.True(target.CanRead); Assert.True(target.CanWrite); Assert.True(target.CanSeek);
        target.Position = 0;
        var verified = await SicherungsarchivPruefung.Pruefe(target, Limits, Excluded, 20_000_000, 65_536, 1_000_000);
        Assert.Equal(1, verified.Formatversion);
        Assert.Equal(Created, verified.ErstelltUtc);
        Assert.Equal(manifest.Eintraege.ToArray(), verified.Eintraege.ToArray());
        Assert.Equal(3, verified.Eintraege.Count);
        target.Position = 0;
        using var zip = new ZipArchive(target, ZipArchiveMode.Read, leaveOpen: true);
        Assert.Equal(ExpectedZipEntries, zip.Entries.Select(e => e.FullName));
        Assert.Equal(File.ReadAllBytes(snapshot.SnapshotPfad), ReadEntry(zip.GetEntry("andytours.db")!));
        Assert.Equal(new byte[] { 1, 2, 3 }, ReadEntry(zip.GetEntry("2026/ride.fit")!));
        Assert.Equal(new byte[] { 4 }, ReadEntry(zip.GetEntry("orphan.bin")!));
        foreach (var pair in sourceBefore) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
    }

    [Fact]
    public async Task RejectsForgedMissingAndChangedSourcesAndPreservesCallerStream()
    {
        using var snapshot = SicherungsdatenbankSnapshot.Erstellen(Db, Stage, Limits);
        var files = SicherungsquellenInventar.Ermittle(_root, Path.Combine(_root, "Sicherungen"), snapshot.ReferenzierteRelativePfade,
            new FileInfo(snapshot.SnapshotPfad).Length, Limits);
        using var missingTarget = new MemoryStream();
        var missing = files.Where(f => f.RelativerPfad != "2026/ride.fit").ToArray();
        await AssertInvalid(() => SicherungsarchivPaketierung.Erstellen(missingTarget, _root, snapshot, missing, Created, Limits, Excluded, 20_000_000, 65_536));
        Assert.Equal(0, missingTarget.Length);
        using var forgedTarget = new MemoryStream();
        var forged = files.Select(f => f with { VollstaendigerPfad = Path.Combine(Path.GetDirectoryName(_root)!, "outside.fit") }).ToArray();
        await AssertInvalid(() => SicherungsarchivPaketierung.Erstellen(forgedTarget, _root, snapshot, forged, Created, Limits, Excluded, 20_000_000, 65_536));
        Assert.Equal(0, forgedTarget.Length);
        var ride = Path.Combine(_root, "2026", "ride.fit"); File.WriteAllBytes(ride, [8, 8, 8, 8]);
        using var changedTarget = new MemoryStream();
        var ex = await Assert.ThrowsAsync<StammdatenException>(() => SicherungsarchivPaketierung.Erstellen(changedTarget, _root, snapshot, files, Created, Limits, Excluded, 20_000_000, 65_536));
        Assert.Equal(StammdatenFehlerart.Konflikt, ex.Art); Assert.True(changedTarget.CanWrite);
        Assert.Equal(new byte[] { 8, 8, 8, 8 }, File.ReadAllBytes(ride));
    }

    [Fact]
    public async Task DetectsDatabaseCommitAfterSnapshot()
    {
        using var snapshot = SicherungsdatenbankSnapshot.Erstellen(Db, Stage, Limits);
        var files = SicherungsquellenInventar.Ermittle(_root, Path.Combine(_root, "Sicherungen"), snapshot.ReferenzierteRelativePfade,
            new FileInfo(snapshot.SnapshotPfad).Length, Limits);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Db, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()))
        {
            connection.Open();
            using var transaction = connection.BeginTransaction();
            EinstellungTabelle.Schreiben(connection, transaction, "packer.change", "changed");
            transaction.Commit();
        }
        using var target = new MemoryStream();
        var error = await Assert.ThrowsAsync<StammdatenException>(() => SicherungsarchivPaketierung.Erstellen(target, _root, snapshot, files, Created, Limits, Excluded, 20_000_000, 65_536));
        Assert.Equal(StammdatenFehlerart.Konflikt, error.Art);
        Assert.Equal(0, target.Length);
    }

    [Fact]
    public async Task EnforcesArchiveAndManifestCapsAndCancellationWithoutTakingStreamOwnership()
    {
        using var snapshot = SicherungsdatenbankSnapshot.Erstellen(Db, Stage, Limits);
        var files = SicherungsquellenInventar.Ermittle(_root, Path.Combine(_root, "Sicherungen"), snapshot.ReferenzierteRelativePfade,
            new FileInfo(snapshot.SnapshotPfad).Length, Limits);
        using var occupied = new MemoryStream(); occupied.WriteByte(9); occupied.Position = 0;
        await AssertInvalid(() => SicherungsarchivPaketierung.Erstellen(occupied, _root, snapshot, files, Created, Limits, Excluded, 20_000_000, 65_536));
        Assert.Equal(new byte[] { 9 }, occupied.ToArray());
        using var smallArchive = new MemoryStream();
        await AssertInvalid(() => SicherungsarchivPaketierung.Erstellen(smallArchive, _root, snapshot, files, Created, Limits, Excluded, 8, 65_536));
        Assert.True(smallArchive.CanWrite);
        using var smallManifest = new MemoryStream();
        await AssertInvalid(() => SicherungsarchivPaketierung.Erstellen(smallManifest, _root, snapshot, files, Created, Limits, Excluded, 20_000_000, 1));
        Assert.True(smallManifest.CanWrite);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        using var cancelled = new MemoryStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SicherungsarchivPaketierung.Erstellen(cancelled, _root, snapshot, files, Created, Limits, Excluded, 20_000_000, 65_536, cts.Token));
        Assert.Equal(0, cancelled.Length); Assert.True(cancelled.CanWrite);
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry) { using var input = entry.Open(); using var output = new MemoryStream(); input.CopyTo(output); return output.ToArray(); }
    private static async Task AssertInvalid(Func<Task<Sicherungsmanifest>> operation)
    {
        var error = await Assert.ThrowsAsync<StammdatenException>(operation);
        Assert.Equal(StammdatenFehlerart.Ungueltig, error.Art);
        Assert.Equal("Sicherungsarchiv konnte nicht erstellt werden.", error.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path.GetDirectoryName(_root))) Directory.Delete(Path.GetDirectoryName(_root)!, true);
        GC.SuppressFinalize(this);
    }
}
