using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Erstellt das kleine Receipt-Register für idempotente Mehrfach-Tourenaktionen.</summary>
internal static class TourenMehrfachSchema
{
    /// <summary>Erstellt die Tabelle innerhalb der aufrufenden Transaktion; besitzt oder committet sie nicht.</summary>
    internal static void Ensure(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ApiTourMehrfach (
                AnfrageId TEXT NOT NULL PRIMARY KEY,
                AnfrageHash TEXT NOT NULL CHECK(length(AnfrageHash) = 64),
                AntwortJson TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }
}
