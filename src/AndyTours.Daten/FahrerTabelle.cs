using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>
/// Liest und schreibt die Tabelle <c>Fahrer</c>.
/// </summary>
public static class FahrerTabelle
{
    /// <summary>
    /// Liest alle Sportler, sortiert nach Nummer.
    /// </summary>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <returns>Die Sportler.</returns>
    public static List<Fahrer> Laden(SqliteConnection verbindung)
    {
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.CommandText = """
            SELECT Id, Vorname, Nachname, Geburtstag, GroesseCm, GewichtKg, Ruhepuls, Maxpuls, Heimatort
            FROM Fahrer ORDER BY Id;
            """;

        var fahrer = new List<Fahrer>();
        using SqliteDataReader leser = befehl.ExecuteReader();
        while (leser.Read())
        {
            // Die Spaltennummern entsprechen der Reihenfolge im SELECT oben.
            fahrer.Add(new Fahrer
            {
                Id = leser.GetInt64(0),
                Vorname = leser.GetString(1),
                Nachname = leser.GetString(2),
                Geburtstag = DatumSpalte.Lesen(leser, 3),
                GroesseCm = leser.GetInt32(4),
                GewichtKg = leser.GetDouble(5),
                Ruhepuls = leser.GetInt32(6),
                Maxpuls = leser.GetInt32(7),
                Heimatort = leser.GetString(8),
            });
        }

        return fahrer;
    }

    /// <summary>
    /// Fügt alle übergebenen Sportler mit ihrer vorhandenen Nummer ein.
    /// </summary>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <param name="transaktion">Laufende Transaktion.</param>
    /// <param name="fahrer">Die Sportler.</param>
    public static void Speichern(SqliteConnection verbindung, SqliteTransaction transaktion, IEnumerable<Fahrer> fahrer)
    {
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.Transaction = transaktion;
        befehl.CommandText = """
            INSERT INTO Fahrer (Id, Vorname, Nachname, Geburtstag, GroesseCm, GewichtKg, Ruhepuls, Maxpuls, Heimatort)
            VALUES ($id, $vorname, $nachname, $geburtstag, $groesse, $gewicht, $ruhepuls, $maxpuls, $heimatort);
            """;

        foreach (Fahrer f in fahrer)
        {
            // Parameter je Zeile neu setzen (Clear), damit keine Werte der vorigen Zeile stehen bleiben.
            befehl.Parameters.Clear();
            befehl.Parameters.AddWithValue("$id", f.Id);
            befehl.Parameters.AddWithValue("$vorname", f.Vorname);
            befehl.Parameters.AddWithValue("$nachname", f.Nachname);
            befehl.Parameters.AddWithValue("$geburtstag", DatumSpalte.AlsText(f.Geburtstag));
            befehl.Parameters.AddWithValue("$groesse", f.GroesseCm);
            befehl.Parameters.AddWithValue("$gewicht", f.GewichtKg);
            befehl.Parameters.AddWithValue("$ruhepuls", f.Ruhepuls);
            befehl.Parameters.AddWithValue("$maxpuls", f.Maxpuls);
            befehl.Parameters.AddWithValue("$heimatort", f.Heimatort);
            befehl.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Speichert die persönlichen Daten in den Sportler von AndyTours; fehlt er noch, wird er angelegt.
    /// </summary>
    /// <remarks>
    /// Geschrieben werden die Werte des Fensters „Persönliche Daten“ (Vorname, Nachname, Geburtstag,
    /// Größe, Gewicht, Ruhe- und Maximalpuls, Heimatort). Alles läuft in einer Transaktion: Schlägt ein Schritt fehl,
    /// bleibt die Datenbank wie vorher.
    /// </remarks>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <param name="fahrer">Die persönlichen Daten; die Nummer wird nicht ausgewertet.</param>
    /// <returns>Die Nummer des Sportlers (bei einem neu angelegten die vergebene).</returns>
    public static long SpeicherePersoenlicheDaten(SqliteConnection verbindung, Fahrer fahrer)
    {
        using SqliteTransaction transaktion = verbindung.BeginTransaction();
        long id = SpeicherePersoenlicheDaten(verbindung, transaktion, fahrer);
        transaktion.Commit();
        return id;
    }

    /// <summary>Speichert persönliche Daten innerhalb der aufrufenden Transaktion.</summary>
    public static long SpeicherePersoenlicheDaten(SqliteConnection verbindung, SqliteTransaction transaktion, Fahrer fahrer)
    {
        long id = Standardfahrer(verbindung, transaktion);
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.Transaction = transaktion;
        befehl.CommandText = """
            UPDATE Fahrer SET
                Vorname = $vorname, Nachname = $nachname, Geburtstag = $geburtstag, GroesseCm = $groesse,
                GewichtKg = $gewicht, Ruhepuls = $ruhepuls, Maxpuls = $maxpuls, Heimatort = $heimatort
            WHERE Id = $id;
            """;
        befehl.Parameters.AddWithValue("$id", id);
        befehl.Parameters.AddWithValue("$vorname", fahrer.Vorname);
        befehl.Parameters.AddWithValue("$nachname", fahrer.Nachname);
        befehl.Parameters.AddWithValue("$geburtstag", DatumSpalte.AlsText(fahrer.Geburtstag));
        befehl.Parameters.AddWithValue("$groesse", fahrer.GroesseCm);
        befehl.Parameters.AddWithValue("$gewicht", fahrer.GewichtKg);
        befehl.Parameters.AddWithValue("$ruhepuls", fahrer.Ruhepuls);
        befehl.Parameters.AddWithValue("$maxpuls", fahrer.Maxpuls);
        befehl.Parameters.AddWithValue("$heimatort", fahrer.Heimatort);
        befehl.ExecuteNonQuery();

        return id;
    }

    /// <summary>
    /// Liefert die Nummer des Sportlers, dem neue Trainingseinheiten zugeordnet werden; legt bei Bedarf einen an.
    /// </summary>
    /// <remarks>
    /// AndyTours kennt nur einen Sportler. Nach der Übernahme der Altdaten ist er vorhanden; in einem neuen,
    /// leeren Datenordner fehlt er noch. Dann wird ein Sportler ohne Angaben angelegt, damit die Einheit
    /// gespeichert werden kann (jede Einheit verweist auf einen Sportler). Seine Daten pflegt der Benutzer
    /// später unter „Persönliche Daten“.
    /// </remarks>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <param name="transaktion">Laufende Transaktion.</param>
    /// <returns>Die Nummer des ersten Sportlers.</returns>
    public static long Standardfahrer(SqliteConnection verbindung, SqliteTransaction transaktion)
    {
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.Transaction = transaktion;
        befehl.CommandText = "SELECT Id FROM Fahrer ORDER BY Id LIMIT 1;";
        if (befehl.ExecuteScalar() is long vorhanden)
        {
            return vorhanden;
        }

        // Noch kein Sportler: einen leeren anlegen, die Nummer vergibt die Datenbank
        befehl.CommandText = """
            INSERT INTO Fahrer (Vorname, Nachname, GroesseCm, GewichtKg, Ruhepuls, Maxpuls, Heimatort)
            VALUES ('', '', 0, 0, 0, 0, '');
            SELECT last_insert_rowid();
            """;
        return (long)befehl.ExecuteScalar()!;
    }
}
