using System.Globalization;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Dauerhafte Zuordnung offizieller Garmin-Aktivitäts-IDs zu importierten Trainingseinheiten.</summary>
public static class GarminImportZuordnung
{
    private const string Vorsilbe = "GarminConnect.Aktivitaet.";

    public static HashSet<long> Laden(SqliteConnection verbindung, ISet<long> vorhandeneEinheiten)
    {
        var ids = new HashSet<long>();
        foreach ((string schluessel, string wert) in EinstellungTabelle.LesenMitVorsilbe(verbindung, Vorsilbe))
        {
            if (long.TryParse(wert, NumberStyles.None, CultureInfo.InvariantCulture, out long einheitId)
                && vorhandeneEinheiten.Contains(einheitId)
                && long.TryParse(schluessel.AsSpan(Vorsilbe.Length), NumberStyles.None, CultureInfo.InvariantCulture, out long id))
                ids.Add(id);
        }
        return ids;
    }

    /// <summary>Removes only Garmin activity mappings that point to tours being deleted; joins caller transaction.</summary>
    public static void EntferneFuerEinheiten(SqliteConnection verbindung, SqliteTransaction transaktion, ISet<long> ids)
    {
        ArgumentNullException.ThrowIfNull(verbindung);
        ArgumentNullException.ThrowIfNull(transaktion);
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0) return;

        var keys = new List<string>();
        using (var select = verbindung.CreateCommand())
        {
            select.Transaction = transaktion;
            select.CommandText = "SELECT Schluessel,Wert FROM Einstellung;";
            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                var key = reader.GetString(0);
                if (key.StartsWith(Vorsilbe, StringComparison.Ordinal) &&
                    long.TryParse(reader.GetString(1), NumberStyles.None, CultureInfo.InvariantCulture, out var tourId) &&
                    ids.Contains(tourId))
                    keys.Add(key);
            }
        }

        using var delete = verbindung.CreateCommand();
        delete.Transaction = transaktion;
        delete.CommandText = "DELETE FROM Einstellung WHERE Schluessel=@key;";
        var parameter = delete.Parameters.Add("@key", SqliteType.Text);
        foreach (var key in keys)
        {
            parameter.Value = key;
            delete.ExecuteNonQuery();
        }
    }

    public static void Speichern(SqliteConnection verbindung, SqliteTransaction transaktion, long aktivitaetId, long einheitId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(aktivitaetId);
        string schluessel = Vorsilbe + aktivitaetId.ToString(CultureInfo.InvariantCulture);
        using (SqliteCommand pruefung = verbindung.CreateCommand())
        {
            pruefung.Transaction = transaktion;
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
                throw new InvalidOperationException("Diese Garmin-Aktivität wurde bereits importiert. Bitte den Datenbestand neu öffnen.");
        }
        EinstellungTabelle.Schreiben(verbindung, transaktion, schluessel, einheitId.ToString(CultureInfo.InvariantCulture));
    }
}
