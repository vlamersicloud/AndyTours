namespace AndyTours.Daten.Datensicherung;

using System.Security;
using AndyTours.Application.Stammdaten;

/// <summary>
/// Owns one isolated staging directory, a claim created with CreateNew, and its database file.
/// Requires trusted pre-provisioned private roots, a caller-held outer lease, and independent exclusion
/// of external writers. This is not a SQLite check, restore proof, or hostile-filesystem TOCTOU defense.
/// Instances require serialized lifecycle calls.
/// </summary>
public sealed class WiederherstellungsPruefarbeitsbereich : IDisposable
{
    private const string InvalidText = "Prüfarbeitsbereich konnte nicht vorbereitet werden.";
    private const string BusyText = "Prüfarbeitsbereich ist bereits belegt.";
    private readonly string _stage;
    private readonly string _databasePath;
    private readonly string _claimPath;
    private FileStream? _database;
    private FileStream? _claim;
    private bool _sealed;
    private bool _disposed;
    private bool _ownsDatabase;
    private bool _ownsStage;

    private WiederherstellungsPruefarbeitsbereich(string stage, string databasePath, string claimPath,
        FileStream database, FileStream claim)
    {
        _stage = stage; _databasePath = databasePath; _claimPath = claimPath;
        _database = database; _claim = claim; _ownsDatabase = true; _ownsStage = true;
    }

    public string StagingOrdner { get { ThrowIfDisposed(); return _stage; } }
    public Stream DatenbankZiel { get { ThrowIfDisposed(); if (_sealed) throw new InvalidOperationException(); return _database!; } }
    public string DatenbankPfad { get { ThrowIfDisposed(); if (!_sealed) throw new InvalidOperationException(); return _databasePath; } }

    public static WiederherstellungsPruefarbeitsbereich Oeffnen(
        string festerPruefordner, string aktiverDatenordner, Guid pruefId, CancellationToken cancellationToken = default)
    {
        FileStream? claim = null;
        FileStream? database = null;
        string? claimPath = null, stage = null, databasePath = null;
        bool ownsStage = false, ownsDatabase = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(festerPruefordner) || !Path.IsPathFullyQualified(festerPruefordner) ||
                string.IsNullOrWhiteSpace(aktiverDatenordner) || !Path.IsPathFullyQualified(aktiverDatenordner) ||
                pruefId == Guid.Empty) throw Invalid();
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(festerPruefordner));
            var active = Path.TrimEndingDirectorySeparator(Path.GetFullPath(aktiverDatenordner));
            if (!IsExistingDirectory(root) || !IsExistingDirectory(active) ||
                HasReparseAncestor(root) || HasReparseAncestor(active) || Overlaps(root, active)) throw Invalid();
            cancellationToken.ThrowIfCancellationRequested();

            claimPath = Path.Combine(root, $".andytours-check-{pruefId:N}.claim");
            try { claim = CreatePrivateFile(claimPath); }
            catch (IOException)
            {
                if (ExistsAny(claimPath)) throw Busy();
                throw Invalid();
            }
            cancellationToken.ThrowIfCancellationRequested();

            stage = Path.Combine(root, $".andytours-check-{pruefId:N}");
            if (ExistsAny(stage)) throw Busy();
            CreatePrivateDirectory(stage);
            ownsStage = true;
            cancellationToken.ThrowIfCancellationRequested();

            databasePath = Path.Combine(stage, "andytours.db");
            try { database = CreatePrivateFile(databasePath); ownsDatabase = true; }
            catch (IOException)
            {
                if (ExistsAny(databasePath)) throw Busy();
                throw Invalid();
            }
            cancellationToken.ThrowIfCancellationRequested();

            var result = new WiederherstellungsPruefarbeitsbereich(stage, databasePath, claimPath, database, claim);
            database = null; claim = null; ownsDatabase = false; ownsStage = false;
            return result;
        }
        catch (OperationCanceledException) { Cleanup(database, databasePath, ownsDatabase, stage, ownsStage, claim, claimPath); throw; }
        catch (StammdatenException) { Cleanup(database, databasePath, ownsDatabase, stage, ownsStage, claim, claimPath); throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                                    NotSupportedException or SecurityException)
        { Cleanup(database, databasePath, ownsDatabase, stage, ownsStage, claim, claimPath); throw Invalid(); }
    }

    public async Task SchliesseAusgabe(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_sealed) return;
        cancellationToken.ThrowIfCancellationRequested();
        var stream = _database!;
        await stream.FlushAsync(cancellationToken);
        await stream.DisposeAsync();
        _database = null;
        _sealed = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cleanup(_database, _databasePath, _ownsDatabase, _stage, _ownsStage, _claim, _claimPath);
        _database = null; _claim = null; _ownsDatabase = false; _ownsStage = false;
        GC.SuppressFinalize(this);
    }

    private static void Cleanup(FileStream? database, string? databasePath, bool ownsDatabase, string? stage, bool ownsStage,
        FileStream? claim, string? claimPath)
    {
        Close(database); Close(claim);
        if (ownsDatabase && databasePath is not null) DeleteFile(databasePath);
        if (ownsStage && stage is not null) DeleteEmptyDirectory(stage);
        if (claim is not null && claimPath is not null) DeleteFile(claimPath);
    }

    private static bool IsExistingDirectory(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.Directory) != 0; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    private static bool Overlaps(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return SamePath(left, right) || IsAncestor(left, right, comparison) || IsAncestor(right, left, comparison);
    }

    private static bool IsAncestor(string parent, string child, StringComparison comparison)
    {
        var prefix = Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar;
        return child.StartsWith(prefix, comparison);
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

    private static bool SamePath(string left, string right) => string.Equals(left, right,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

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

    private static void Close(FileStream? stream)
    {
        if (stream is null) return;
        try { stream.Dispose(); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (SecurityException) { }
        catch (ObjectDisposedException) { }
    }

    private static void DeleteFile(string path)
    {
        try { if (ExistsAny(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (SecurityException) { }
    }

    private static void DeleteEmptyDirectory(string path)
    {
        try { if (ExistsAny(path)) Directory.Delete(path, recursive: false); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (SecurityException) { }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, InvalidText);
    private static StammdatenException Busy() => new(StammdatenFehlerart.Konflikt, BusyText);
}
