using System.Globalization;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten.Datensicherung;

/// <summary>
/// Legt eine vollständige Kopie der Datenbank im Sicherungsordner an.
/// </summary>
public static class ErstelleSicherung
{
    /// <summary>
    /// Sichert die Datenbankdatei in den Sicherungsordner; der Name enthält Datum und Uhrzeit.
    /// </summary>
    /// <remarks>
    /// Ablauf:
    /// <list type="number">
    /// <item>Sicherungsordner bei Bedarf anlegen.</item>
    /// <item>Die Zwischendatei <c>…db.tmp</c> mit .NET anlegen. Fehlende Schreibrechte fallen so
    /// als <see cref="UnauthorizedAccessException"/> auf und nicht erst als allgemeiner SQLite-Fehler.</item>
    /// <item>Die Online-Sicherung von SQLite (<see cref="SqliteConnection.BackupDatabase(SqliteConnection)"/>)
    /// schreibt die Datenbank Seite für Seite in die Zwischendatei. Anders als ein einfaches
    /// Kopieren der Datei ist das Ergebnis auch dann in sich stimmig, wenn die Datenbank gerade
    /// geöffnet ist.</item>
    /// <item>Erst danach wird die Zwischendatei in den endgültigen Namen umbenannt. Eine abgebrochene
    /// Kopie gilt so nie als Sicherung; bei einem Fehler wird die Zwischendatei entfernt.</item>
    /// </list>
    /// Beide Verbindungen laufen ohne Verbindungs-Wiederverwendung (<c>Pooling = false</c>), damit
    /// die Dateien nach dem Schließen sofort wieder frei sind (Umbenennen, Löschen).
    /// </remarks>
    /// <param name="datenbankdatei">Vollständiger Pfad der zu sichernden Datenbank.</param>
    /// <param name="sicherungsordner">Zielordner der Sicherung.</param>
    /// <param name="zeitpunkt">Zeitpunkt für den Namen (beim Programmstart: jetzt).</param>
    /// <returns>Vollständiger Pfad der angelegten Sicherung.</returns>
    /// <exception cref="IOException">Ordner nicht erreichbar, Datenträger voll o. Ä.</exception>
    /// <exception cref="UnauthorizedAccessException">Keine Schreibrechte im Sicherungsordner.</exception>
    /// <exception cref="SqliteException">Die Datenbank konnte nicht gelesen oder die Kopie nicht geschrieben werden.</exception>
    public static string Ausfuehren(string datenbankdatei, string sicherungsordner, DateTime zeitpunkt)
    {
        Directory.CreateDirectory(sicherungsordner);

        string name = Sicherungsdatei.Praefix
            + zeitpunkt.ToString(Sicherungsdatei.Zeitstempelformat, CultureInfo.InvariantCulture)
            + Sicherungsdatei.Endung;
        string ziel = Path.Combine(sicherungsordner, name);
        string zwischendatei = ziel + Sicherungsdatei.Zwischenendung;

        try
        {
            // Leere Datei anlegen: prüft die Schreibrechte; SQLite füllt sie anschließend
            using (File.Create(zwischendatei))
            {
            }

            using (var quelle = new SqliteConnection(Sicherungsdatei.Verbindungstext(datenbankdatei, SqliteOpenMode.ReadOnly)))
            using (var kopie = new SqliteConnection(Sicherungsdatei.Verbindungstext(zwischendatei, SqliteOpenMode.ReadWrite)))
            {
                quelle.Open();
                kopie.Open();
                quelle.BackupDatabase(kopie);
            }

            // Zwei Sicherungen in derselben Sekunde haben denselben Stand – die neuere ersetzt die ältere
            File.Move(zwischendatei, ziel, overwrite: true);
        }
        catch
        {
            Sicherungsdatei.LoescheZwischendatei(zwischendatei);
            throw;
        }

        return ziel;
    }
}
