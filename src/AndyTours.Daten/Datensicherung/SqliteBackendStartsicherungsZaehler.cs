namespace AndyTours.Daten.Datensicherung;

using System.Globalization;
using AndyTours.Application.Sicherung;
using Microsoft.Data.Sqlite;

/// <summary>
/// Existing-file-only SQLite counter store. Each write transaction changes only
/// Sicherung.StartsSeitSicherung; no schema creation, migration, policy rewrite, or retention.
/// </summary>
public sealed class SqliteBackendStartsicherungsZaehler : IBackendStartsicherungsZaehler
{
    private readonly string _databasePath;

    public SqliteBackendStartsicherungsZaehler(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = Path.GetFullPath(databasePath);
    }

    public Task<BackendStartsicherungsStand> ZaehlenUndErhoehen(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OeffneReadWriteExisting();
        using var transaction = connection.BeginTransaction(deferred: false);
        cancellationToken.ThrowIfCancellationRequested();

        var intervalText = ReadSetting(connection, transaction, Einstellungsschluessel.Sicherungsintervall);
        var interval = string.IsNullOrWhiteSpace(intervalText)
            ? new AndyTours.Core.Modell.Sicherungseinstellungen().Intervall
            : int.TryParse(intervalText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedInterval) &&
              parsedInterval is >= 1 and <= 99
                ? parsedInterval
                : throw new InvalidDataException("Sicherungsrichtlinie ist ungültig.");

        var counterText = ReadSetting(connection, transaction, Einstellungsschluessel.StartsSeitSicherung);
        var previous = string.IsNullOrWhiteSpace(counterText) ? 0 :
            int.TryParse(counterText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedCounter) &&
            parsedCounter >= 0 ? parsedCounter : throw new InvalidDataException("Sicherungszähler ist ungültig.");

        using var countCommand = connection.CreateCommand();
        countCommand.Transaction = transaction;
        countCommand.CommandText = "SELECT COUNT(*) FROM Trainingseinheit;";
        var units = checked(Convert.ToInt32(countCommand.ExecuteScalar(), CultureInfo.InvariantCulture));
        var next = previous == int.MaxValue ? int.MaxValue : previous + 1;

        cancellationToken.ThrowIfCancellationRequested();
        WriteCounter(connection, transaction, next);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return Task.FromResult(new BackendStartsicherungsStand(interval, next, units));
    }

    public Task<bool> Zuruecksetzen(int erwarteterStand, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(erwarteterStand);
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OeffneReadWriteExisting();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE Einstellung SET Wert = '0'
            WHERE Schluessel = $key AND Wert = $expected;
            """;
        command.Parameters.AddWithValue("$key", Einstellungsschluessel.StartsSeitSicherung);
        command.Parameters.AddWithValue("$expected", erwarteterStand.ToString(CultureInfo.InvariantCulture));
        cancellationToken.ThrowIfCancellationRequested();
        var changed = command.ExecuteNonQuery() == 1;
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return Task.FromResult(changed);
    }

    private static string? ReadSetting(SqliteConnection connection, SqliteTransaction transaction, string key)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Wert FROM Einstellung WHERE Schluessel = $key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    private static void WriteCounter(SqliteConnection connection, SqliteTransaction transaction, int value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Einstellung (Schluessel, Wert) VALUES ($key, $value)
            ON CONFLICT(Schluessel) DO UPDATE SET Wert = excluded.Wert;
            """;
        command.Parameters.AddWithValue("$key", Einstellungsschluessel.StartsSeitSicherung);
        command.Parameters.AddWithValue("$value", value.ToString(CultureInfo.InvariantCulture));
        if (command.ExecuteNonQuery() != 1)
            throw new InvalidDataException("Sicherungszähler konnte nicht gespeichert werden.");
    }

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
            if (string.IsNullOrEmpty(parent) ||
                string.Equals(parent, current, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                break;
            current = parent;
        }
        return false;
    }

    private SqliteConnection OeffneReadWriteExisting()
    {
        if (!File.Exists(_databasePath)) throw new FileNotFoundException("Datenbank nicht verfügbar.");
        if (HasReparseAncestor(_databasePath))
            throw new IOException("Datenbank nicht verfügbar.");
        var attributes = File.GetAttributes(_databasePath);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new IOException("Datenbank nicht verfügbar.");
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
