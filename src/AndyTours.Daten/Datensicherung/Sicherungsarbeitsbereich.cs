namespace AndyTours.Daten.Datensicherung;

using System.Security;
using AndyTours.Application.Stammdaten;

/// <summary>
/// Owns one private operation workspace and a successfully created claim. Requires a trusted server backup folder
/// and caller-held outer exclusive dataset lease. This is not a global process lock or hostile-filesystem TOCTOU defense.
/// </summary>
public sealed class Sicherungsarbeitsbereich : IDisposable
{
    private const string InvalidText = "Sicherungsarbeitsbereich konnte nicht vorbereitet werden.";
    private const string BusyText = "Sicherungsvorgang ist bereits belegt.";
    private readonly string _stage;
    private readonly string _databaseDirectory;
    private readonly string _archiveTemp;
    private readonly string _finalPath;
    private readonly string _claimPath;
    private FileStream? _claim;
    private bool _disposed;

    private Sicherungsarbeitsbereich(string stage, string databaseDirectory, string archiveTemp, string finalPath,
        string claimPath, FileStream claim)
    {
        _stage = stage; _databaseDirectory = databaseDirectory; _archiveTemp = archiveTemp;
        _finalPath = finalPath; _claimPath = claimPath; _claim = claim;
    }

    public string StagingOrdner { get { ThrowIfDisposed(); return _stage; } }
    public string DatenbankOrdner { get { ThrowIfDisposed(); return _databaseDirectory; } }
    public string ArchivZwischendatei { get { ThrowIfDisposed(); return _archiveTemp; } }
    public string Zieldatei { get { ThrowIfDisposed(); return _finalPath; } }

    public static Sicherungsarbeitsbereich Oeffnen(string backupOrdner, Guid vorgangsId, CancellationToken cancellationToken = default)
    {
        FileStream? ownedClaim = null;
        string? ownedStage = null;
        string? ownedClaimPath = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(backupOrdner) || !Path.IsPathFullyQualified(backupOrdner) || vorgangsId == Guid.Empty)
                throw Invalid();
            var backup = Path.TrimEndingDirectorySeparator(Path.GetFullPath(backupOrdner));
            if (HasReparseAncestor(backup)) throw Invalid();
            if (ExistsAny(backup))
            {
                if ((File.GetAttributes(backup) & FileAttributes.Directory) == 0) throw Invalid();
            }
            else CreatePrivateDirectory(backup);
            if (HasReparseAncestor(backup)) throw Invalid();
            cancellationToken.ThrowIfCancellationRequested();

            var candidateClaim = Path.Combine(backup, $".andytours-claim-{vorgangsId:N}");
            try
            {
                ownedClaim = CreatePrivateFile(candidateClaim);
                ownedClaimPath = candidateClaim;
            }
            catch (IOException)
            {
                if (ExistsAny(candidateClaim)) throw Busy();
                throw Invalid();
            }

            var stage = Path.Combine(backup, $".andytours-stage-{vorgangsId:N}");
            var finalPath = Zielpfad(backup, vorgangsId);
            if (ExistsAny(stage) || ExistsAny(finalPath)) throw Busy();
            CreatePrivateDirectory(stage);
            ownedStage = stage;
            var dbDirectory = Path.Combine(stage, "db");
            CreatePrivateDirectory(dbDirectory);
            var tempArchive = Path.Combine(stage, "archive.zip.tmp");
            cancellationToken.ThrowIfCancellationRequested();
            var result = new Sicherungsarbeitsbereich(stage, dbDirectory, tempArchive, finalPath, ownedClaimPath, ownedClaim);
            ownedClaim = null; ownedClaimPath = null; ownedStage = null;
            return result;
        }
        catch (OperationCanceledException)
        {
            CloseOwnedClaim(ownedClaim); DeleteOwnedStage(ownedStage); DeleteOwnedClaimFile(ownedClaimPath); throw;
        }
        catch (StammdatenException)
        {
            CloseOwnedClaim(ownedClaim); DeleteOwnedStage(ownedStage); DeleteOwnedClaimFile(ownedClaimPath); throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                                        NotSupportedException or SecurityException)
        {
            CloseOwnedClaim(ownedClaim); DeleteOwnedStage(ownedStage); DeleteOwnedClaimFile(ownedClaimPath); throw Invalid();
        }
    }

    internal static string Zielpfad(string backupOrdner, Guid vorgangsId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupOrdner);
        ArgumentOutOfRangeException.ThrowIfEqual(vorgangsId, Guid.Empty);
        return Path.Combine(Path.GetFullPath(backupOrdner), $"andytours_full_{vorgangsId:N}.zip");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseOwnedClaim(_claim); _claim = null;
        DeleteOwnedStage(_stage);
        DeleteOwnedClaimFile(_claimPath);
        GC.SuppressFinalize(this);
    }

    private static FileStream CreatePrivateFile(string path)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(path, options);
    }

    private static void CreatePrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
        else Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static bool ExistsAny(string path)
    {
        try { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    private static bool HasReparseAncestor(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true; }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || SamePath(parent, current)) break;
            current = parent;
        }
        return false;
    }

    private static bool SamePath(string a, string b) => string.Equals(a, b,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void CloseOwnedClaim(FileStream? claim)
    {
        if (claim is null) return;
        try { claim.Dispose(); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (SecurityException) { }
        catch (ObjectDisposedException) { }
    }

    private static void DeleteOwnedClaimFile(string? path)
    {
        if (path is null) return;
        try { if (ExistsAny(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (SecurityException) { }
    }

    private static void DeleteOwnedStage(string? path)
    {
        if (path is null) return;
        try { if (ExistsAny(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (SecurityException) { }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, InvalidText);
    private static StammdatenException Busy() => new(StammdatenFehlerart.Konflikt, BusyText);
}
