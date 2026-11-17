namespace AndyTours.Tests.Sicherung;

using AndyTours.Application.Stammdaten;
using AndyTours.Daten.Datensicherung;
using Xunit;

public sealed class WiederherstellungsPruefarbeitsbereichTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "andy-restore-workspace-tests", Guid.NewGuid().ToString("N"));
    private string Root => Path.Combine(_base, "prüfung");
    private string Active => Path.Combine(_base, "active");
    private static readonly Guid Id = Guid.Parse("0d443a6f-224e-42b6-83ca-e98ccf29c963");
    private string Stage => Path.Combine(Root, $".andytours-check-{Id:N}");
    private string Claim => Path.Combine(Root, $".andytours-check-{Id:N}.claim");

    public WiederherstellungsPruefarbeitsbereichTests()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Active);
    }

    [Fact]
    public async Task SealedWorkspaceDisposesOwnedFilesAndPreservesUnrelatedData()
    {
        var unrelated = Path.Combine(Root, "keep.txt");
        File.WriteAllText(unrelated, "keep");
        var activeSentinel = Path.Combine(Active, "active.db");
        File.WriteAllBytes(activeSentinel, [8, 7, 6]);
        using var workspace = WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, Active, Id);
        Assert.Throws<InvalidOperationException>(() => _ = workspace.DatenbankPfad);
        var bytes = new byte[] { 4, 5, 6 };
        await workspace.DatenbankZiel.WriteAsync(bytes);
        await workspace.SchliesseAusgabe();
        await workspace.SchliesseAusgabe();
        Assert.Equal(bytes, await File.ReadAllBytesAsync(workspace.DatenbankPfad));
        Assert.Throws<InvalidOperationException>(() => _ = workspace.DatenbankZiel);
        workspace.Dispose();
        workspace.Dispose();
        Assert.False(File.Exists(Path.Combine(Stage, "andytours.db")));
        Assert.False(Directory.Exists(Stage));
        Assert.False(File.Exists(Claim));
        Assert.Equal("keep", File.ReadAllText(unrelated));
        Assert.Equal(new byte[] { 8, 7, 6 }, File.ReadAllBytes(activeSentinel));
        Assert.True(Directory.Exists(Active));
    }

    [Fact]
    public void DisposeBeforeSealRemovesOnlyOwnedArtifacts()
    {
        using var workspace = WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, Active, Id);
        workspace.Dispose();
        Assert.False(Directory.Exists(Stage));
        Assert.False(File.Exists(Claim));
        Assert.Throws<ObjectDisposedException>(() => _ = workspace.StagingOrdner);
    }

    [Fact]
    public void ExistingClaimIsPreservedExactly()
    {
        File.WriteAllText(Claim, "foreign claim");
        var before = File.ReadAllBytes(Claim);
        var ex = Assert.Throws<StammdatenException>(() => WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, Active, Id));
        Assert.Equal(StammdatenFehlerart.Konflikt, ex.Art);
        Assert.Equal(before, File.ReadAllBytes(Claim));
        Assert.False(Directory.Exists(Stage));
    }

    [Fact]
    public void ActiveClaimConflictDoesNotStealFirstWorkspace()
    {
        using var first = WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, Active, Id);
        var ex = Assert.Throws<StammdatenException>(() => WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, Active, Id));
        Assert.Equal(StammdatenFehlerart.Konflikt, ex.Art);
        Assert.True(File.Exists(Claim));
        Assert.True(File.Exists(Path.Combine(Stage, "andytours.db")));
        Assert.True(Directory.Exists(first.StagingOrdner));
    }

    [Fact]
    public void ExistingStageForeignFileIsPreservedAndNewClaimRemoved()
    {
        Directory.CreateDirectory(Stage);
        var marker = Path.Combine(Stage, "foreign.txt");
        File.WriteAllText(marker, "untouched");
        var ex = Assert.Throws<StammdatenException>(() => WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, Active, Id));
        Assert.Equal(StammdatenFehlerart.Konflikt, ex.Art);
        Assert.Equal("untouched", File.ReadAllText(marker));
        Assert.False(File.Exists(Claim));
    }

    [Fact]
    public void DisposeLeavesUnexpectedForeignFileInsideOwnedStage()
    {
        using var workspace = WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, Active, Id);
        var foreign = Path.Combine(workspace.StagingOrdner, "foreign.txt");
        File.WriteAllText(foreign, "preserve");
        workspace.Dispose();
        Assert.Equal("preserve", File.ReadAllText(foreign));
        Assert.True(Directory.Exists(Stage));
        Assert.False(File.Exists(Claim));
    }

    [Fact]
    public void UnixPermissionsArePrivateAndExistingRootsAreUnchanged()
    {
        if (OperatingSystem.IsWindows()) return;
        var rootFile = Path.Combine(Root, "root-keep.txt");
        File.WriteAllText(rootFile, "root");
        var rootMode = File.GetUnixFileMode(Root);
        var fileMode = File.GetUnixFileMode(rootFile);
        using var workspace = WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, Active, Id);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Stage));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(Stage, "andytours.db")));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Claim));
        workspace.Dispose();
        Assert.Equal(rootMode, File.GetUnixFileMode(Root));
        Assert.Equal(fileMode, File.GetUnixFileMode(rootFile));
    }

    [Fact]
    public async Task CancelledSealLeavesDatabaseOpenForRetry()
    {
        using var workspace = WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, Active, Id);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workspace.SchliesseAusgabe(cts.Token));
        Assert.Throws<InvalidOperationException>(() => _ = workspace.DatenbankPfad);
        await workspace.DatenbankZiel.WriteAsync(new byte[] { 9 });
        await workspace.SchliesseAusgabe();
        Assert.True(File.Exists(workspace.DatenbankPfad));
    }

    [Fact]
    public void FilesystemRootIsRejectedWithoutArtifacts()
    {
        var fileSystemRoot = Path.GetPathRoot(Root)!;
        var ex = Assert.Throws<StammdatenException>(() =>
            WiederherstellungsPruefarbeitsbereich.Oeffnen(fileSystemRoot, Active, Id));
        Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Root));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Active));
    }

    [Fact]
    public void RootAndActiveSymlinkAliasesAreRejected()
    {
        if (OperatingSystem.IsWindows()) return;
        var rootTarget = Path.Combine(_base, "root-target");
        var activeTarget = Path.Combine(_base, "active-target");
        Directory.CreateDirectory(rootTarget);
        Directory.CreateDirectory(activeTarget);
        var rootAlias = Path.Combine(_base, "root-alias");
        var activeAlias = Path.Combine(_base, "active-alias");
        Directory.CreateSymbolicLink(rootAlias, rootTarget);
        Directory.CreateSymbolicLink(activeAlias, activeTarget);
        foreach (var pair in new[] { (rootAlias, Active), (Root, activeAlias), (Path.Combine(_base, "ancestor-alias"), Active) })
        {
            if (pair.Item1.EndsWith("ancestor-alias", StringComparison.Ordinal))
            {
                Directory.CreateSymbolicLink(pair.Item1, _base);
                var exAncestor = Assert.Throws<StammdatenException>(() =>
                    WiederherstellungsPruefarbeitsbereich.Oeffnen(Path.Combine(pair.Item1, "prüfung"), pair.Item2, Id));
                Assert.Equal(StammdatenFehlerart.Ungueltig, exAncestor.Art);
            }
            else
            {
                var ex = Assert.Throws<StammdatenException>(() =>
                    WiederherstellungsPruefarbeitsbereich.Oeffnen(pair.Item1, pair.Item2, Id));
                Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
            }
        }
        Assert.Empty(Directory.EnumerateFileSystemEntries(Root));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Active));
    }

    [Fact]
    public void SymlinkClaimAndStageArePreservedWithTargetsUntouched()
    {
        if (OperatingSystem.IsWindows()) return;
        var claimTarget = Path.Combine(_base, "claim-target");
        File.WriteAllText(claimTarget, "claim sentinel");
        File.CreateSymbolicLink(Claim, claimTarget);
        var claimError = Assert.Throws<StammdatenException>(() => WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, Active, Id));
        Assert.Equal(StammdatenFehlerart.Konflikt, claimError.Art);
        Assert.True(File.Exists(Claim));
        Assert.Equal("claim sentinel", File.ReadAllText(claimTarget));
        File.Delete(Claim);

        var stageTarget = Path.Combine(_base, "stage-target");
        Directory.CreateDirectory(stageTarget);
        var marker = Path.Combine(stageTarget, "foreign.txt");
        File.WriteAllText(marker, "stage sentinel");
        Directory.CreateSymbolicLink(Stage, stageTarget);
        var stageError = Assert.Throws<StammdatenException>(() => WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, Active, Id));
        Assert.Equal(StammdatenFehlerart.Konflikt, stageError.Art);
        Assert.True(Directory.Exists(Stage));
        Assert.Equal("stage sentinel", File.ReadAllText(marker));
        Assert.False(File.Exists(Claim));
    }

    [Fact]
    public void InvalidRootsAndCancellationCreateNoArtifacts()
    {
        Assert.Throws<StammdatenException>(() => WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, Active, Guid.Empty));
        Assert.Throws<StammdatenException>(() => WiederherstellungsPruefarbeitsbereich.Oeffnen(Path.Combine(_base, "missing-check"), Active, Id));
        Assert.Throws<StammdatenException>(() => WiederherstellungsPruefarbeitsbereich.Oeffnen("relative", Active, Id));
        Assert.Throws<StammdatenException>(() => WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, Path.Combine(_base, "missing"), Id));
        Assert.Throws<StammdatenException>(() => WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, Root, Id));
        Assert.Throws<StammdatenException>(() => WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, _base, Id));
        Assert.Throws<StammdatenException>(() => WiederherstellungsPruefarbeitsbereich.Oeffnen(_base, Active, Id));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => WiederherstellungsPruefarbeitsbereich.Oeffnen(Root, Active, Id, cts.Token));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Root));
    }

    public void Dispose()
    {
        if (Directory.Exists(_base)) Directory.Delete(_base, recursive: true);
        GC.SuppressFinalize(this);
    }
}
