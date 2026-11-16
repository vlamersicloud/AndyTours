using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

internal static class FitUploadSchema
{
    internal static void Ensure(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS FitUpload (
                UploadId TEXT NOT NULL PRIMARY KEY,
                Dateiname TEXT NOT NULL,
                InhaltHash TEXT NOT NULL CHECK(length(InhaltHash) = 64),
                Byteanzahl INTEGER NOT NULL CHECK(Byteanzahl >= 0 AND Byteanzahl <= 134217728),
                Revision INTEGER NOT NULL CHECK(Revision > 0),
                Zustand INTEGER NOT NULL CHECK(Zustand BETWEEN 0 AND 2),
                TourId INTEGER REFERENCES Trainingseinheit(Id) ON DELETE SET NULL,
                ErstelltUtc TEXT NOT NULL,
                AktualisiertUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_FitUpload_TourId ON FitUpload(TourId);

            CREATE TABLE IF NOT EXISTS FitUploadQuittung (
                AnfrageId TEXT NOT NULL PRIMARY KEY,
                AnfrageHash TEXT NOT NULL,
                AntwortJson TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }
}
