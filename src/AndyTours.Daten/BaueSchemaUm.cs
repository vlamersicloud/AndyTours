using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>
/// Passt eine Datenbank einer älteren Schemaversion an das aktuelle Schema an.
/// </summary>
/// <remarks>
/// Vorgehen nach <c>docs/adr/0010-anpassung-des-datenbankschemas.md</c>: Die Umbaustufen laufen der Reihe
/// nach, beginnend bei der Version der Datenbank, alle in einer Transaktion. Die neue Versionsnummer wird
/// erst am Ende gesetzt. Schlägt eine Stufe fehl, bleibt die Datenbank vollständig im alten Stand. Die
/// Sicherung vor dem Umbau legt <see cref="OeffneDatenbank"/> an.
/// </remarks>
public static class BaueSchemaUm
{
    /// <summary>
    /// Umbaustufe auf Version 5: entfernt Werte aus den Altdaten, die AndyTours nirgends verwendet
    /// (Gewicht, Radumfang, Beiwerte, Kettenblätter, Ritzel und Bild am Equipment, Trainingspuls und
    /// Koordinaten des Heimatorts am Fahrer, die Tabelle der Schwellwerte).
    /// </summary>
    /// <remarks><c>DROP COLUMN</c> gibt es in SQLite seit Version 3.35.</remarks>
    private const string StufeVersion5 = """
        ALTER TABLE Equipment DROP COLUMN GewichtKg;
        ALTER TABLE Equipment DROP COLUMN RadumfangMm;
        ALTER TABLE Equipment DROP COLUMN CrCoeff;
        ALTER TABLE Equipment DROP COLUMN CmCoeff;
        ALTER TABLE Equipment DROP COLUMN CwCoeff;
        ALTER TABLE Equipment DROP COLUMN CwaCoeff;
        ALTER TABLE Equipment DROP COLUMN SinCoeff;
        ALTER TABLE Equipment DROP COLUMN Kettenblaetter;
        ALTER TABLE Equipment DROP COLUMN Ritzel;
        ALTER TABLE Equipment DROP COLUMN Bildpfad;
        ALTER TABLE Fahrer DROP COLUMN TrainingspulsVon;
        ALTER TABLE Fahrer DROP COLUMN TrainingspulsBis;
        ALTER TABLE Fahrer DROP COLUMN HeimatBreitengrad;
        ALTER TABLE Fahrer DROP COLUMN HeimatLaengengrad;
        DROP TABLE IF EXISTS Schwellwert;
        """;

    /// <summary>
    /// Baut die Datenbank von <paramref name="version"/> auf <see cref="ErstelleSchema.Schemaversion"/> um.
    /// </summary>
    /// <remarks>
    /// Zuerst legt <see cref="ErstelleSchema"/> fehlende Tabellen an (bei Datenbanken vor Version 4 z. B. die
    /// Overlays), danach folgen die Umbaustufen, die über der Version der Datenbank liegen.
    /// </remarks>
    /// <param name="verbindung">Geöffnete Verbindung ohne laufende Transaktion.</param>
    /// <param name="version">Bisherige Schemaversion der Datenbank (1 bis Schemaversion − 1).</param>
    /// <exception cref="SqliteException">Eine Stufe ist gescheitert; die Datenbank ist unverändert.</exception>
    public static void Ausfuehren(SqliteConnection verbindung, long version)
    {
        using SqliteTransaction transaktion = verbindung.BeginTransaction();
        ErstelleSchema.Ausfuehren(verbindung, transaktion);

        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.Transaction = transaktion;
        if (version < 5)
        {
            befehl.CommandText = StufeVersion5;
            befehl.ExecuteNonQuery();
        }

        befehl.CommandText = $"PRAGMA user_version = {ErstelleSchema.Schemaversion};";
        befehl.ExecuteNonQuery();

        // Erst hier wird alles endgültig geschrieben; ohne Commit verwirft Dispose den Umbau.
        transaktion.Commit();
    }
}
