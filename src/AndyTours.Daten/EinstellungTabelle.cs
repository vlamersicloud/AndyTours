using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>
/// Liest und schreibt Einträge der Tabelle <c>Einstellung</c> (Schlüssel/Wert als Text).
/// </summary>
/// <remarks>
/// Die Klassen <c>…Einstellung</c> wandeln ihre Werte in Text und zurück; das Lesen und Schreiben der Tabelle
/// geschieht nur hier. Die Schlüssel stehen in <see cref="Einstellungsschluessel"/>.
/// </remarks>
public static class EinstellungTabelle
{
    /// <summary>
    /// Liest den Wert eines Schlüssels.
    /// </summary>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <param name="schluessel">Der Schlüssel, z. B. „Oberflaeche.Groesse“.</param>
    /// <returns>Der Wert oder <c>null</c>, wenn der Schlüssel fehlt.</returns>
    public static string? Lesen(SqliteConnection verbindung, string schluessel)
    {
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.CommandText = "SELECT Wert FROM Einstellung WHERE Schluessel = $schluessel;";
        befehl.Parameters.AddWithValue("$schluessel", schluessel);
        return befehl.ExecuteScalar() as string;
    }

    /// <summary>
    /// Liest alle Einträge, deren Schlüssel mit der Vorsilbe beginnt (alle Einträge bei leerer Vorsilbe).
    /// </summary>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <param name="vorsilbe">Die Vorsilbe des Bereichs, z. B. „Routing.“.</param>
    /// <returns>Die Einträge als Schlüssel → Wert.</returns>
    public static Dictionary<string, string> LesenMitVorsilbe(SqliteConnection verbindung, string vorsilbe)
    {
        var werte = new Dictionary<string, string>(StringComparer.Ordinal);
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.CommandText = "SELECT Schluessel, Wert FROM Einstellung;";
        using SqliteDataReader leser = befehl.ExecuteReader();
        while (leser.Read())
        {
            // Vergleich im Programm statt mit LIKE, weil „_“ und „%“ in LIKE Platzhalter wären
            string schluessel = leser.GetString(0);
            if (schluessel.StartsWith(vorsilbe, StringComparison.Ordinal))
            {
                werte[schluessel] = leser.GetString(1);
            }
        }

        return werte;
    }

    /// <summary>
    /// Schreibt den Wert eines Schlüssels; <c>null</c> löscht den Eintrag.
    /// </summary>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <param name="transaktion">Laufende Transaktion oder <c>null</c>.</param>
    /// <param name="schluessel">Der Schlüssel.</param>
    /// <param name="wert">Der Wert oder <c>null</c>.</param>
    public static void Schreiben(SqliteConnection verbindung, SqliteTransaction? transaktion, string schluessel, string? wert)
    {
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.Transaction = transaktion;
        befehl.CommandText = wert is null
            ? "DELETE FROM Einstellung WHERE Schluessel = $schluessel;"
            : "INSERT OR REPLACE INTO Einstellung (Schluessel, Wert) VALUES ($schluessel, $wert);";
        befehl.Parameters.AddWithValue("$schluessel", schluessel);
        if (wert is not null)
        {
            befehl.Parameters.AddWithValue("$wert", wert);
        }

        befehl.ExecuteNonQuery();
    }
}
