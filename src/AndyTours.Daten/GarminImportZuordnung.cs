using System.Globalization;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Dauerhafte Zuordnung offizieller Garmin-Aktivitäts-IDs zu importierten Trainingseinheiten.</summary>
public static class GarminImportZuordnung
{
    private const string Vorsilbe = "GarminConnect.Aktivitaet.";

    /// <summary>Liest nur Zuordnungen zu noch existierenden Einheiten; gelöschte Touren können erneut importiert werden.</summary>
    public static HashSet<long> Laden(SqliteConnection verbindung, ISet<long> vorhandeneEinheiten)
    {
        var ids = new HashSet<long>();
        foreach ((string schluessel, string wert) in EinstellungTabelle.LesenMitVorsilbe(verbindung, Vorsilbe))
        {
            if (long.TryParse(wert, NumberStyles.None, CultureInfo.InvariantCulture, out long einheitId)
                && vorhandeneEinheiten.Contains(einheitId)
                && long.TryParse(schluessel.AsSpan(Vorsilbe.Length), NumberStyles.None, CultureInfo.InvariantCulture, out long id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>Schreibt die ID innerhalb derselben Transaktion wie die Trainingseinheit, niemals schon beim Download.</summary>
    public static void Speichern(SqliteConnection verbindung, SqliteTransaction transaktion, long aktivitaetId, long einheitId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(aktivitaetId);
        string schluessel = Vorsilbe + aktivitaetId.ToString(CultureInfo.InvariantCulture);
        using (SqliteCommand pruefung = verbindung.CreateCommand())
        {
            pruefung.Transaction = transaktion;
            // Innerhalb derselben Schreibtransaktion prüfen: Auch ein zweites Programm mit
            // veraltetem Speicherbestand darf dieselbe ID keiner zweiten Einheit zuordnen.
            pruefung.CommandText = """
                SELECT EXISTS(
                    SELECT 1 FROM Einstellung e
                    JOIN Trainingseinheit t ON CAST(t.Id AS TEXT) = e.Wert
                    WHERE e.Schluessel = $schluessel AND t.Id <> $einheitId
                );
                """;
            pruefung.Parameters.AddWithValue("$schluessel", schluessel);
            pruefung.Parameters.AddWithValue("$einheitId", einheitId);
            if ((long)pruefung.ExecuteScalar()! != 0)
            {
                throw new InvalidOperationException("Diese Garmin-Aktivität wurde bereits importiert. Bitte den Datenbestand neu öffnen.");
            }
        }

        EinstellungTabelle.Schreiben(verbindung, transaktion, schluessel, einheitId.ToString(CultureInfo.InvariantCulture));
    }
}
