namespace AndyTours.Daten.Datensicherung;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Security;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using Microsoft.Data.Sqlite;

/// <summary>Owned SQLite snapshot plus a readonly observer for detecting in-process-observable commits.</summary>
public sealed class SicherungsdatenbankSnapshot : IDisposable
{
    private const string Fehlertext = "Datenbanksicherung konnte nicht vorbereitet werden.";
    private const string Konflikttext = "Datenbestand wurde während der Sicherung geändert.";
    private SqliteConnection? _observer;
    private readonly long _initialDataVersion;
    private readonly string _snapshotPath;
    private bool _disposed;

    private SicherungsdatenbankSnapshot(string snapshotPath, SqliteConnection observer, long initialDataVersion,
        IReadOnlyList<string> referencedPaths)
    {
        _snapshotPath = snapshotPath;
        _observer = observer;
        _initialDataVersion = initialDataVersion;
        ReferenzierteRelativePfade = referencedPaths;
    }

    /// <summary>Server-side staging path; never expose this value to API clients.</summary>
    public string SnapshotPfad { get { ThrowIfDisposed(); return _snapshotPath; } }

    public IReadOnlyList<string> ReferenzierteRelativePfade { get; }

    /// <summary>Creates one snapshot in an existing empty private staging directory. Requires caller-held exclusive dataset lease; this is not cross-process protection.</summary>
    public static SicherungsdatenbankSnapshot Erstellen(string quellDatenbank, string stagingOrdner,
        SicherungsmanifestGrenzen grenzen, CancellationToken cancellationToken = default)
    {
        string? createdPath = null;
        SqliteConnection? observer = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(quellDatenbank) || string.IsNullOrWhiteSpace(stagingOrdner) ||
                !Path.IsPathFullyQualified(quellDatenbank) || !Path.IsPathFullyQualified(stagingOrdner) ||
                grenzen is null || grenzen.MaxEintraege <= 0 || grenzen.MaxDateiBytes <= 0 ||
                grenzen.MaxGesamtBytes <= 0 || grenzen.MaxPfadzeichen <= 0)
                throw Invalid();
            var source = Path.GetFullPath(quellDatenbank);
            var staging = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagingOrdner));
            if (!File.Exists(source) || HasReparseAncestor(source) || !Directory.Exists(staging) || HasReparseAncestor(staging) ||
                Directory.EnumerateFileSystemEntries(staging).Any()) throw Invalid();

            var snapshotPath = Path.Combine(staging, "andytours.db");
            using (new FileStream(snapshotPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            createdPath = snapshotPath;
            cancellationToken.ThrowIfCancellationRequested();

            observer = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = source, Mode = SqliteOpenMode.ReadOnly, Pooling = false
            }.ToString());
            observer.Open();
            var initialVersion = ScalarInt64(observer, "PRAGMA data_version;");
            var pages = ScalarInt64(observer, "PRAGMA page_count;");
            var pageSize = ScalarInt64(observer, "PRAGMA page_size;");
            var estimatedBytes = checked(pages * pageSize);
            if (pages <= 0 || pageSize <= 0 || estimatedBytes > grenzen.MaxDateiBytes || estimatedBytes > grenzen.MaxGesamtBytes)
                throw Invalid();
            cancellationToken.ThrowIfCancellationRequested();

            using (var target = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = snapshotPath, Mode = SqliteOpenMode.ReadWrite, Pooling = false
            }.ToString()))
            {
                target.Open();
                observer.BackupDatabase(target);
            }
            cancellationToken.ThrowIfCancellationRequested();
            var actualBytes = new FileInfo(snapshotPath).Length;
            if (actualBytes <= 0 || actualBytes > grenzen.MaxDateiBytes || actualBytes > grenzen.MaxGesamtBytes)
                throw Invalid();
            cancellationToken.ThrowIfCancellationRequested();
            if (PruefeSicherung.Ausfuehren(snapshotPath) != Sicherungspruefung.Brauchbar) throw Invalid();
            cancellationToken.ThrowIfCancellationRequested();

            var references = ReadAttachmentPaths(snapshotPath, grenzen, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var result = new SicherungsdatenbankSnapshot(snapshotPath, observer, initialVersion,
                new ReadOnlyCollection<string>(references));
            observer = null;
            createdPath = null;
            return result;
        }
        catch (OperationCanceledException) { observer?.Dispose(); DeleteOwned(createdPath); throw; }
        catch (StammdatenException) { observer?.Dispose(); DeleteOwned(createdPath); throw Invalid(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or
                                        ArgumentException or OverflowException or InvalidCastException or
                                        NotSupportedException or SecurityException)
        { observer?.Dispose(); DeleteOwned(createdPath); throw Invalid(); }
    }

    public void PruefeUnveraendert(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            var observer = _observer ?? throw Invalid();
            if (ScalarInt64(observer, "PRAGMA data_version;") != _initialDataVersion)
                throw new StammdatenException(StammdatenFehlerart.Konflikt, Konflikttext);
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw; }
        catch (Exception ex) when (ex is SqliteException or InvalidCastException or OverflowException)
        { throw Invalid(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _observer?.Dispose();
        _observer = null;
        DeleteOwned(_snapshotPath);
        GC.SuppressFinalize(this);
    }

    private static List<string> ReadAttachmentPaths(string path, SicherungsmanifestGrenzen limits, CancellationToken ct)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT RelativerPfad, length(CAST(RelativerPfad AS BLOB)) FROM Anhang ORDER BY Id;";
        using var reader = command.ExecuteReader();
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            ct.ThrowIfCancellationRequested();
            if (reader.IsDBNull(0) || reader.IsDBNull(1) || reader.GetInt64(1) > 4L * limits.MaxPfadzeichen) throw Invalid();
            var raw = reader.GetString(0);
            if (raw.Length == 0 || raw.Length > limits.MaxPfadzeichen) throw Invalid();
            if (seen.Add(raw))
            {
                if (results.Count >= limits.MaxEintraege) throw Invalid();
                results.Add(raw);
            }
        }
        return results;
    }

    private static long ScalarInt64(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
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
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) break;
            current = parent;
        }
        return false;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static void DeleteOwned(string? path)
    {
        if (path is null) return;
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, Fehlertext);
}
