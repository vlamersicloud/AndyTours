using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>
/// Liest und schreibt die Tabelle <c>Equipment</c> samt Wartungsintervallen.
/// </summary>
public static class EquipmentsTabelle
{
    /// <summary>
    /// Liest alle Equipments (auch ausgemusterte), sortiert nach Nummer.
    /// </summary>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <returns>Die Equipments.</returns>
    public static List<Equipment> Laden(SqliteConnection verbindung)
    {
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.CommandText = """
            SELECT Id, Name, AktivitaetId, Geraetezuordnung, StandardKategorie, IstStandard,
                   Ausgemustert, Anschaffungsdatum, AnfangsdistanzKm
            FROM Equipment ORDER BY Id;
            """;

        var equipments = new List<Equipment>();
        using SqliteDataReader leser = befehl.ExecuteReader();
        while (leser.Read())
        {
            equipments.Add(new Equipment
            {
                Id = leser.GetInt64(0),
                Name = leser.GetString(1),
                AktivitaetId = leser.IsDBNull(2) ? null : leser.GetInt64(2),
                Geraetezuordnung = leser.GetString(3),
                StandardKategorie = leser.GetString(4),
                IstStandard = leser.GetBoolean(5),
                Ausgemustert = leser.GetBoolean(6),
                Anschaffungsdatum = DatumSpalte.Lesen(leser, 7),
                AnfangsdistanzKm = leser.GetDouble(8),
            });
        }

        return equipments;
    }

    /// <summary>
    /// Fügt alle übergebenen Equipments mit ihrer vorhandenen Nummer ein.
    /// </summary>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <param name="transaktion">Laufende Transaktion.</param>
    /// <param name="equipments">Die Equipments.</param>
    public static void Speichern(SqliteConnection verbindung, SqliteTransaction transaktion, IEnumerable<Equipment> equipments)
    {
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.Transaction = transaktion;
        befehl.CommandText = """
            INSERT INTO Equipment (Id, Name, AktivitaetId, Geraetezuordnung, StandardKategorie, IstStandard,
                                   Ausgemustert, Anschaffungsdatum, AnfangsdistanzKm)
            VALUES ($id, $name, $aktivitaetId, $zuordnung, $standardKategorie, $istStandard,
                    $ausgemustert, $anschaffung, $anfangsdistanz);
            """;

        foreach (Equipment e in equipments)
        {
            befehl.Parameters.Clear();
            befehl.Parameters.AddWithValue("$id", e.Id);
            SetzeWerte(befehl, e);
            befehl.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Ersetzt die Standardkategorie aller Equipments, die den alten Namen tragen – ohne Unterschied
    /// zwischen Groß- und Kleinschreibung und ohne Leerzeichen am Rand.
    /// </summary>
    /// <remarks>
    /// Die Standardkategorie ist als Name gespeichert; beim Umbenennen und Zusammenführen von Kategorien
    /// muss sie deshalb eigens mitgezogen werden.
    /// </remarks>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <param name="transaktion">Laufende Transaktion.</param>
    /// <param name="alt">Bisheriger Name der Kategorie.</param>
    /// <param name="neu">Neuer Name.</param>
    /// <returns>Anzahl der geänderten Equipments.</returns>
    public static int ErsetzeStandardKategorie(SqliteConnection verbindung, SqliteTransaction transaktion, string alt, string neu)
    {
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.Transaction = transaktion;
        befehl.CommandText = "UPDATE Equipment SET StandardKategorie = $neu WHERE TRIM(StandardKategorie) = $alt COLLATE NOCASE;";
        befehl.Parameters.AddWithValue("$alt", alt.Trim());
        befehl.Parameters.AddWithValue("$neu", neu.Trim());
        return befehl.ExecuteNonQuery();
    }

    /// <summary>
    /// Fügt ein neues Equipment ein; die Datenbank vergibt die Nummer.
    /// </summary>
    /// <param name="verbindung">Offene Verbindung.</param>
    /// <param name="transaktion">Laufende Transaktion.</param>
    /// <param name="equipment">Das Equipment; seine Nummer wird nicht verwendet.</param>
    /// <returns>Die vergebene Nummer.</returns>
    public static long Einfuegen(SqliteConnection verbindung, SqliteTransaction transaktion, Equipment equipment)
    {
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.Transaction = transaktion;
        befehl.CommandText = """
            INSERT INTO Equipment (Name, AktivitaetId, Geraetezuordnung, StandardKategorie, IstStandard,
                                   Ausgemustert, Anschaffungsdatum, AnfangsdistanzKm)
            VALUES ($name, $aktivitaetId, $zuordnung, $standardKategorie, $istStandard,
                    $ausgemustert, $anschaffung, $anfangsdistanz)
            RETURNING Id;
            """;
        SetzeWerte(befehl, equipment);
        return (long)befehl.ExecuteScalar()!;
    }

    /// <summary>
    /// Schreibt alle Felder eines vorhandenen Equipments; die Zuordnung der Einheiten und Wartungen
    /// (über die Nummer) bleibt erhalten.
    /// </summary>
    /// <param name="verbindung">Offene Verbindung.</param>
    /// <param name="transaktion">Laufende Transaktion.</param>
    /// <param name="equipment">Das geänderte Equipment.</param>
    public static void Aendern(SqliteConnection verbindung, SqliteTransaction transaktion, Equipment equipment)
    {
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.Transaction = transaktion;
        befehl.CommandText = """
            UPDATE Equipment SET
                Name = $name, AktivitaetId = $aktivitaetId, Geraetezuordnung = $zuordnung,
                StandardKategorie = $standardKategorie, IstStandard = $istStandard, Ausgemustert = $ausgemustert,
                Anschaffungsdatum = $anschaffung, AnfangsdistanzKm = $anfangsdistanz
            WHERE Id = $id;
            """;
        befehl.Parameters.AddWithValue("$id", equipment.Id);
        SetzeWerte(befehl, equipment);
        befehl.ExecuteNonQuery();
    }

    /// <summary>
    /// Löscht ein Equipment. Verwenden es noch Einheiten oder Wartungen, scheitert das am Fremdschlüssel;
    /// die Oberfläche prüft das vorher mit der Löschsperre.
    /// </summary>
    /// <param name="verbindung">Offene Verbindung.</param>
    /// <param name="transaktion">Laufende Transaktion.</param>
    /// <param name="id">Nummer des Equipments.</param>
    public static void Loeschen(SqliteConnection verbindung, SqliteTransaction transaktion, long id)
    {
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.Transaction = transaktion;
        befehl.CommandText = "DELETE FROM Equipment WHERE Id = $id;";
        befehl.Parameters.AddWithValue("$id", id);
        befehl.ExecuteNonQuery();
    }

    /// <summary>Setzt die Parameter aller Felder außer der Nummer.</summary>
    private static void SetzeWerte(SqliteCommand befehl, Equipment e)
    {
        befehl.Parameters.AddWithValue("$name", e.Name);
        befehl.Parameters.AddWithValue("$aktivitaetId", (object?)e.AktivitaetId ?? DBNull.Value);
        befehl.Parameters.AddWithValue("$zuordnung", e.Geraetezuordnung);
        befehl.Parameters.AddWithValue("$standardKategorie", e.StandardKategorie);
        befehl.Parameters.AddWithValue("$istStandard", e.IstStandard);
        befehl.Parameters.AddWithValue("$ausgemustert", e.Ausgemustert);
        befehl.Parameters.AddWithValue("$anschaffung", DatumSpalte.AlsText(e.Anschaffungsdatum));
        befehl.Parameters.AddWithValue("$anfangsdistanz", e.AnfangsdistanzKm);
    }
}
