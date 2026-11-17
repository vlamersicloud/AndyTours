namespace AndyTours.Tests.Sicherung;

using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Daten.Datensicherung;
using Xunit;

public sealed class SicherungsquellenInventarTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "andy-source-inventory-tests", Guid.NewGuid().ToString("N"));
    private string Root => Path.Combine(_root, "data");
    private string Backup => Path.Combine(Root, "backup-custom");
    private static readonly string[] ExpectedPaths = ["a.fit", "orphan.bin"];
    private static SicherungsmanifestGrenzen Limits => new(3, 10, 14, 200);

    public SicherungsquellenInventarTests()
    {
        Directory.CreateDirectory(Root);
        File.WriteAllBytes(Path.Combine(Root, "andytours.db"), new byte[10]);
        File.WriteAllBytes(Path.Combine(Root, "a.fit"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(Root, "orphan.bin"), [4]);
        Directory.CreateDirectory(Path.Combine(Root, "Sicherungen"));
        File.WriteAllBytes(Path.Combine(Root, "Sicherungen", "old.db"), [1]);
        Directory.CreateDirectory(Backup);
        File.WriteAllBytes(Path.Combine(Backup, "archive.zip"), [1]);
        Directory.CreateDirectory(Path.Combine(Root, ".upload-stage"));
        File.WriteAllBytes(Path.Combine(Root, ".upload-stage", "part"), [1]);
        File.WriteAllBytes(Path.Combine(Root, "temp.tmp"), [1]);
        File.WriteAllBytes(Path.Combine(Root, "andytours.db-wal"), [1]);
        File.WriteAllBytes(Path.Combine(Root, "andytours.db-shm"), [1]);
        File.WriteAllBytes(Path.Combine(Root, "andytours.db-journal"), [1]);
    }

    [Fact]
    public void InventoryIsSortedIncludesOrphansExcludesKnownStagingAndDoesNotWrite()
    {
        var before = Snapshot(Root);
        var files = SicherungsquellenInventar.Ermittle(Root, Backup, ["a.fit", "a.fit"], 10, Limits);
        Assert.Equal(ExpectedPaths, files.Select(x => x.RelativerPfad));
        Assert.Equal(14L, files.Sum(x => x.Byteanzahl) + 10);
        Assert.Equal(before, Snapshot(Root));
    }

    [Theory]
    [InlineData("missing.fit")]
    [InlineData("../outside.fit")]
    [InlineData("andytours.db")]
    [InlineData("temp.tmp")]
    [InlineData("Sicherungen/old.db")]
    [InlineData("backup-custom/archive.zip")]
    [InlineData(".upload-stage/part")]
    [InlineData("A.fit")]
    public void UnmatchedOrExcludedReferenceFailsClosed(string reference)
    {
        var ex = Assert.Throws<StammdatenException>(() => SicherungsquellenInventar.Ermittle(Root, Backup, [reference], 10, Limits));
        Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
        Assert.Equal("Sicherungsquellen ungültig.", ex.Message);
    }

    [Fact]
    public void EnforcesEntryTotalAndDatabaseFileLimitsWithoutDoubleCounting()
    {
        Assert.Throws<StammdatenException>(() => SicherungsquellenInventar.Ermittle(Root, Backup, [], 10, new(2, 10, 14, 200)));
        Assert.Throws<StammdatenException>(() => SicherungsquellenInventar.Ermittle(Root, Backup, [], 10, new(3, 10, 13, 200)));
        Assert.Throws<StammdatenException>(() => SicherungsquellenInventar.Ermittle(Root, Backup, [], 11, new(3, 10, 14, 200)));
    }

    [Fact]
    public void RejectsRelativeRootsAndRootEqualsBackupAndDoesNotCreateBackupDirectory()
    {
        Assert.Throws<StammdatenException>(() => SicherungsquellenInventar.Ermittle("relative", Backup, [], 10, Limits));
        Assert.Throws<StammdatenException>(() => SicherungsquellenInventar.Ermittle(Root, "relative-backup", [], 10, Limits));
        Assert.Throws<StammdatenException>(() => SicherungsquellenInventar.Ermittle(Root, Root, [], 10, Limits));
        Directory.Delete(Backup, recursive: true);
        var missingBackup = Path.Combine(Root, "not-created");
        _ = SicherungsquellenInventar.Ermittle(Root, missingBackup, [], 10, Limits);
        Assert.False(Directory.Exists(missingBackup));
    }

    [Fact]
    public void CancellationPropagatesWithoutChangingSourceTree()
    {
        var before = Snapshot(Root);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => SicherungsquellenInventar.Ermittle(Root, Backup, [], 10, Limits, cts.Token));
        Assert.Equal(before, Snapshot(Root));
    }

    [Fact]
    public void RejectsReparseEntriesAndRootAncestorsWhenSupported()
    {
        var target = Path.Combine(_root, "target"); File.WriteAllBytes(target, [1]);
        var link = Path.Combine(Root, "linked.bin");
        try { File.CreateSymbolicLink(link, target); }
        catch (Exception ex) when (ex is PlatformNotSupportedException or UnauthorizedAccessException or IOException) { return; }
        Assert.Throws<StammdatenException>(() => SicherungsquellenInventar.Ermittle(Root, Backup, [], 10, Limits));
        File.Delete(link);
        var alias = Path.Combine(_root, "alias");
        try
        {
            Directory.CreateSymbolicLink(alias, _root);
            Assert.Throws<StammdatenException>(() => SicherungsquellenInventar.Ermittle(
                Path.Combine(alias, "data"), Path.Combine(alias, "data", "backup-custom"), [], 10, Limits));
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or UnauthorizedAccessException or IOException) { }
        finally { if (Directory.Exists(alias)) Directory.Delete(alias); }
    }

    private static string Snapshot(string path) => string.Join("\n", Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories)
        .OrderBy(x => x, StringComparer.Ordinal).Select(x => Path.GetRelativePath(path, x) + ":" +
            (Directory.Exists(x) ? "dir" : Convert.ToHexString(File.ReadAllBytes(x)))));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
