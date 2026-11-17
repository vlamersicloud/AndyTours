namespace AndyTours.Tests.Sicherung;

using AndyTours.Application.Stammdaten;
using AndyTours.Daten.Datensicherung;
using Xunit;

public sealed class SicherungsarbeitsbereichTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "andy-workspace-tests", Guid.NewGuid().ToString("N"));
    private string Backup => Path.Combine(_base, "backups");
    private static readonly Guid OperationId = Guid.Parse("df9a50d0-424f-48bd-946b-a8ec23af55b4");
    private string Claim => Path.Combine(Backup, $".andytours-claim-{OperationId:N}");
    private string Stage => Path.Combine(Backup, $".andytours-stage-{OperationId:N}");
    private string Final => Path.Combine(Backup, $"andytours_full_{OperationId:N}.zip");

    public SicherungsarbeitsbereichTests() => Directory.CreateDirectory(_base);

    [Fact]
    public void CreatesExpectedPrivateWorkspaceAndDisposeCleansOnlyOwnedPaths()
    {
        Directory.CreateDirectory(Backup);
        UnixFileMode? backupMode = !OperatingSystem.IsWindows() ? File.GetUnixFileMode(Backup) : null;
        var unrelated = Path.Combine(Backup, "keep.txt"); File.WriteAllText(unrelated, "keep");
        using var workspace = Sicherungsarbeitsbereich.Oeffnen(Backup, OperationId);
        Assert.Equal(Stage, workspace.StagingOrdner);
        Assert.Equal(Path.Combine(Stage, "db"), workspace.DatenbankOrdner);
        Assert.Equal(Path.Combine(Stage, "archive.zip.tmp"), workspace.ArchivZwischendatei);
        Assert.Equal(Final, workspace.Zieldatei);
        Assert.True(Directory.Exists(workspace.DatenbankOrdner));
        Assert.True(File.Exists(Claim));
        Assert.False(File.Exists(Final));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Stage));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(workspace.DatenbankOrdner));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Claim));
            Assert.Equal(backupMode, File.GetUnixFileMode(Backup));
        }
        File.WriteAllText(Final, "published");
        workspace.Dispose(); workspace.Dispose();
        Assert.False(Directory.Exists(Stage));
        Assert.False(File.Exists(Claim));
        Assert.Equal("keep", File.ReadAllText(unrelated));
        Assert.Equal("published", File.ReadAllText(Final));
        Assert.Throws<ObjectDisposedException>(() => _ = workspace.Zieldatei);
    }

    [Fact]
    public void ExistingStaleClaimIsPreservedExactly()
    {
        Directory.CreateDirectory(Backup);
        File.WriteAllText(Claim, "active claim sentinel");
        var claimBefore = File.ReadAllBytes(Claim);
        var error = Assert.Throws<StammdatenException>(() => Sicherungsarbeitsbereich.Oeffnen(Backup, OperationId));
        Assert.Equal(StammdatenFehlerart.Konflikt, error.Art);
        Assert.Equal("Sicherungsvorgang ist bereits belegt.", error.Message);
        Assert.Equal(claimBefore, File.ReadAllBytes(Claim));
        Assert.False(Directory.Exists(Stage));
    }

    [Fact]
    public void SecondActiveOperationConflictsWithoutStealingFirstClaim()
    {
        using var first = Sicherungsarbeitsbereich.Oeffnen(Backup, OperationId);
        var error = Assert.Throws<StammdatenException>(() => Sicherungsarbeitsbereich.Oeffnen(Backup, OperationId));
        Assert.Equal(StammdatenFehlerart.Konflikt, error.Art);
        Assert.True(File.Exists(Claim));
        Assert.True(Directory.Exists(first.StagingOrdner));
        Assert.True(Directory.Exists(first.DatenbankOrdner));
        first.Dispose();
        Assert.False(File.Exists(Claim));
        Assert.False(Directory.Exists(Stage));
    }

    [Fact]
    public void ExistingStageOrFinalIsPreservedAndOwnedClaimIsRemoved()
    {
        Directory.CreateDirectory(Stage);
        var marker = Path.Combine(Stage, "foreign.txt"); File.WriteAllText(marker, "stage");
        var stageError = Assert.Throws<StammdatenException>(() => Sicherungsarbeitsbereich.Oeffnen(Backup, OperationId));
        Assert.Equal(StammdatenFehlerart.Konflikt, stageError.Art);
        Assert.Equal("stage", File.ReadAllText(marker));
        Assert.False(File.Exists(Claim));
        Directory.Delete(Stage, recursive: true);

        File.WriteAllText(Final, "validated-existing");
        var finalError = Assert.Throws<StammdatenException>(() => Sicherungsarbeitsbereich.Oeffnen(Backup, OperationId));
        Assert.Equal(StammdatenFehlerart.Konflikt, finalError.Art);
        Assert.Equal("validated-existing", File.ReadAllText(Final));
        Assert.False(File.Exists(Claim));
    }

    [Fact]
    public void RejectsInvalidRequestAndCancellationWithoutCreatingClaim()
    {
        Assert.Throws<StammdatenException>(() => Sicherungsarbeitsbereich.Oeffnen("relative", OperationId));
        Assert.Throws<StammdatenException>(() => Sicherungsarbeitsbereich.Oeffnen(Backup, Guid.Empty));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => Sicherungsarbeitsbereich.Oeffnen(Backup, OperationId, cts.Token));
        Assert.False(Directory.Exists(Backup));
    }

    [Fact]
    public void RejectsBackupAncestorSymlinkOnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;
        var target = Path.Combine(_base, "real"); Directory.CreateDirectory(target);
        var alias = Path.Combine(_base, "alias");
        Directory.CreateSymbolicLink(alias, target);
        var error = Assert.Throws<StammdatenException>(() => Sicherungsarbeitsbereich.Oeffnen(Path.Combine(alias, "backups"), OperationId));
        Assert.Equal(StammdatenFehlerart.Ungueltig, error.Art);
        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
        Assert.False(Directory.Exists(Path.Combine(alias, "backups")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_base)) Directory.Delete(_base, recursive: true);
        GC.SuppressFinalize(this);
    }
}
