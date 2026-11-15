using System.Globalization;
using AndyTours.Core.Modell;
using AndyTours.Daten.Datensicherung;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>
/// Öffnet die SQLite-Datenbank von AndyTours.
/// </summary>
public static class OeffneDatenbank
{
    /// <summary>
    /// Öffnet (bzw. erzeugt) die Datenbankdatei und stellt sicher, dass das aktuelle Schema vorhanden ist.
    /// </summary>
    /// <remarks>
    /// SQLite legt die Datei automatisch an, wenn sie noch nicht existiert.
    /// Fremdschlüssel-Prüfungen sind bei SQLite standardmäßig <b>aus</b> und müssen für jede
    /// Verbindung eingeschaltet werden – sonst ließen sich z. B. Einheiten mit einem nicht
    /// vorhandenen Equipment speichern.
    /// <para>
    /// Je nach Schemaversion (<c>PRAGMA user_version</c>):
    /// <list type="bullet">
    /// <item>0 (neue Datei): Tabellen anlegen, aktuelle Version setzen.</item>
    /// <item>Älter als <see cref="ErstelleSchema.Schemaversion"/>: zuerst sichern, dann mit
    /// <see cref="BaueSchemaUm"/> umbauen. Scheitert die Sicherung, wird nichts umgebaut.</item>
    /// <item>Aktuell oder neuer: nur fehlende Tabellen anlegen, die Version bleibt.</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="dateipfad">Pfad zur Datenbankdatei, oder „:memory:" für eine reine Arbeitsspeicher-Datenbank (Tests).</param>
    /// <param name="vorUmbauSichern">
    /// <c>false</c> nur für die Zwischendatei beim Wiederherstellen: Ihre Quelle ist selbst eine Sicherung,
    /// und der aktuelle Stand ist dort schon gesichert.
    /// </param>
    /// <returns>Die geöffnete Verbindung. Der Aufrufer muss sie schließen (<c>using</c>).</returns>
    /// <exception cref="SicherungVorUmbauFehlgeschlagenException">Ältere Datenbank, Sicherung gescheitert.</exception>
    public static SqliteConnection Ausfuehren(string dateipfad, bool vorUmbauSichern = true)
    {
        var verbindungstext = new SqliteConnectionStringBuilder
        {
            DataSource = dateipfad,
            // Keine Verbindungs-Wiederverwendung: Sonst bleibt die Datei nach dem Schließen gesperrt
            // und könnte z. B. für eine erneute Migration nicht gelöscht werden.
            Pooling = false,
        }.ToString();

        var verbindung = new SqliteConnection(verbindungstext);
        try
        {
            verbindung.Open();
            FuehreAus(verbindung, "PRAGMA foreign_keys = ON;");

            long version = Convert.ToInt64(Abfrage(verbindung, "PRAGMA user_version;"), CultureInfo.InvariantCulture);
            if (version > 0 && version < ErstelleSchema.Schemaversion)
            {
                if (vorUmbauSichern)
                {
                    SichereVorUmbau(verbindung, dateipfad);
                }

                BaueSchemaUm.Ausfuehren(verbindung, version);
            }
            else
            {
                ErstelleSchema.Ausfuehren(verbindung);
                if (version == 0)
                {
                    FuehreAus(verbindung, $"PRAGMA user_version = {ErstelleSchema.Schemaversion};");
                }
            }
        }
        catch
        {
            verbindung.Dispose();
            throw;
        }

        return verbindung;
    }

    /// <summary>
    /// Sichert die Datenbank vor dem Umbau in den eingestellten Sicherungsordner, unabhängig vom Intervall.
    /// </summary>
    /// <remarks>
    /// Die Sicherungseinstellungen stehen in der Datenbank selbst; vor Schemaversion 2 gab es die Tabelle
    /// dafür noch nicht, dann gelten die Standardeinstellungen.
    /// </remarks>
    private static void SichereVorUmbau(SqliteConnection verbindung, string dateipfad)
    {
        Sicherungseinstellungen einstellungen =
            Abfrage(verbindung, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Einstellung';") is long anzahl and > 0
                ? SicherungEinstellungen.Laden(verbindung)
                : new Sicherungseinstellungen();
        string datenordner = Path.GetDirectoryName(Path.GetFullPath(dateipfad))!;
        string sicherungsordner = ErmittleSicherungsordner.Ausfuehren(datenordner, einstellungen);

        try
        {
            ErstelleSicherung.Ausfuehren(dateipfad, sicherungsordner, DateTime.Now);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException or SqliteException)
        {
            throw new SicherungVorUmbauFehlgeschlagenException(sicherungsordner,
                BeschreibeSicherungsfehler.Ausfuehren(fehler, FuehreStartsicherungDurch.StandardZeitlimit), fehler);
        }
    }

    /// <summary>Führt eine Anweisung ohne Ergebnis aus.</summary>
    private static void FuehreAus(SqliteConnection verbindung, string sql)
    {
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.CommandText = sql;
        befehl.ExecuteNonQuery();
    }

    /// <summary>Führt eine Abfrage aus und liefert den ersten Wert.</summary>
    private static object? Abfrage(SqliteConnection verbindung, string sql)
    {
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.CommandText = sql;
        return befehl.ExecuteScalar();
    }
}
