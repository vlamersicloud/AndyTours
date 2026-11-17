using System.Globalization;
using AndyTours.Daten;
using AndyTours.Daten.Datensicherung;
using Microsoft.Data.Sqlite;

namespace AndyTours.SqlitePruefer;

/// <summary>Read-only Basisprüfung einer einzelnen lokalen SQLite-Datei.</summary>
public static class SqliteReadonlyPruefer
{
    private static readonly string[] Kerntabellen =
    [
        "Fahrer", "Aktivitaet", "Kategorie", "Equipment", "Trainingseinheit",
        "EinheitKategorie", "Anhang", "Wartung"
    ];

    public static SqlitePruefprotokoll Pruefen(string path)
    {
        try
        {
            if (!Path.IsPathFullyQualified(path) || Directory.Exists(path) || !File.Exists(path) ||
                HatReparsePointImPfad(path))
                return SqlitePruefprotokoll.Ergebnis("invalid_input");

            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString();
            using var connection = new SqliteConnection(cs);
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA query_only = ON;";
                command.ExecuteNonQuery();
                command.CommandText = "PRAGMA query_only;";
                if (Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
                    return SqlitePruefprotokoll.Ergebnis("readonly_unavailable");
            }

            var version = Convert.ToInt32(Scalar(connection, "PRAGMA user_version;"), CultureInfo.InvariantCulture);
            if (version < 1 || version > ErstelleSchema.Schemaversion)
                return SqlitePruefprotokoll.Ergebnis("unsupported_schema", version);

            foreach (var table in Kerntabellen)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
                command.Parameters.AddWithValue("$name", table);
                if (Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
                    return SqlitePruefprotokoll.Ergebnis("core_schema_invalid", version);
            }

            var quickCheck = Convert.ToString(Scalar(connection, "PRAGMA quick_check;"), CultureInfo.InvariantCulture);
            return quickCheck == "ok"
                ? SqlitePruefprotokoll.Ergebnis("ok", version, Kerntabellen.Length, "ok")
                : SqlitePruefprotokoll.Ergebnis("integrity_failed", version, Kerntabellen.Length, "failed");
        }
        catch
        {
            return SqlitePruefprotokoll.Ergebnis("check_failed");
        }
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static bool HatReparsePointImPfad(string path)
    {
        var current = new FileInfo(Path.GetFullPath(path));
        if ((current.Attributes & FileAttributes.ReparsePoint) != 0) return true;
        var directory = current.Directory;
        while (directory is not null)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) return true;
            directory = directory.Parent;
        }
        return false;
    }
}
