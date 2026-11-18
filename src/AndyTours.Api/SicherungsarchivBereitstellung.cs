namespace AndyTours.Api;

using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Daten;
using AndyTours.Daten.Datensicherung;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Lazily resolves fixed server-side full-archive creation dependencies. Call only after the request's
/// outer exclusive dataset lease is held; no external-writer or filesystem-TOCTOU protection is provided.
/// </summary>
public sealed class SicherungsarchivBereitstellung : IBackendStartsicherungsArchiv
{
    private readonly IConfiguration _configuration;
    private readonly string _datenordner;
    private readonly TimeProvider _timeProvider;

    public SicherungsarchivBereitstellung(IConfiguration configuration, string datenordner, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(datenordner);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _configuration = configuration;
        _datenordner = datenordner;
        _timeProvider = timeProvider;
    }

    /// <summary>Returns null for unavailable feature configuration or unsafe/unreadable server folders.</summary>
    public DateisystemSicherungsarchivErstellung? Ermittle(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!SicherungsarchivKonfiguration.TryLesen(_configuration, out var grenzen) || grenzen is null)
            return null;

        try
        {
            if (!Path.IsPathFullyQualified(_datenordner)) return null;
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_datenordner));
            if (!Directory.Exists(root) || HasReparseAncestor(root)) return null;
            var databasePath = Path.Combine(root, "andytours.db");
            if (!File.Exists(databasePath) || HasReparseAncestor(databasePath)) return null;

            string backupFolder;
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString()))
            {
                cancellationToken.ThrowIfCancellationRequested();
                connection.Open();
                using var transaction = connection.BeginTransaction(deferred: true);
                var settings = SicherungEinstellung.Laden(connection);
                backupFolder = ErmittleSicherungsordner.Ausfuehren(root, settings);
                transaction.Commit();
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!Path.IsPathFullyQualified(backupFolder)) return null;
            var fullBackupFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(backupFolder));
            if (SamePath(root, fullBackupFolder) || HasReparseAncestor(fullBackupFolder)) return null;
            try
            {
                var attributes = File.GetAttributes(fullBackupFolder);
                if ((attributes & FileAttributes.Directory) == 0) return null;
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            cancellationToken.ThrowIfCancellationRequested();
            return new DateisystemSicherungsarchivErstellung(root, fullBackupFolder, grenzen, _timeProvider);
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { return null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                                    InvalidOperationException or SqliteException or System.Security.SecurityException or
                                    NotSupportedException)
        {
            return null;
        }
    }

    ISicherungsarchivErstellung? IBackendStartsicherungsArchiv.Ermittle(CancellationToken cancellationToken) =>
        Ermittle(cancellationToken);

    private static bool HasReparseAncestor(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || SamePath(parent, current)) break;
            current = parent;
        }
        return false;
    }

    private static bool SamePath(string first, string second) =>
        string.Equals(first, second, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
