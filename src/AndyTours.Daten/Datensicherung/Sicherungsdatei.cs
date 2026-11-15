using Microsoft.Data.Sqlite;

namespace AndyTours.Daten.Datensicherung;

/// <summary>
/// Feste Bestandteile der Dateinamen von Datensicherungen und gemeinsame Hilfen beim Schreiben der Dateien.
/// </summary>
/// <remarks>
/// Eine Sicherung heißt <c>andytours_JJJJ-MM-TT_hh-mm-ss.db</c>, z. B.
/// <c>andytours_2026-10-23_07-39-12.db</c>. Am Zeitstempel im Namen erkennt die App beim Aufräumen,
/// welche Dateien Sicherungen sind und welche die ältesten sind; alle anderen Dateien im
/// Sicherungsordner bleiben unberührt.
/// </remarks>
public static class Sicherungsdatei
{
    /// <summary>Anfang jedes Sicherungsnamens.</summary>
    public const string Praefix = "andytours_";

    /// <summary>Format des Zeitstempels im Namen (sortiert auch als Text richtig).</summary>
    public const string Zeitstempelformat = "yyyy-MM-dd_HH-mm-ss";

    /// <summary>Dateiendung einer Sicherung.</summary>
    public const string Endung = ".db";

    /// <summary>
    /// Zusätzliche Endung, unter der eine Sicherung geschrieben wird, solange sie noch nicht fertig ist.
    /// Eine abgebrochene Kopie trägt diese Endung und gilt deshalb nie als Sicherung.
    /// </summary>
    public const string Zwischenendung = ".tmp";

    /// <summary>
    /// Verbindungstext für eine Datei mit der angegebenen Zugriffsart, ohne Verbindungs-Wiederverwendung.
    /// </summary>
    /// <remarks>Ohne Wiederverwendung ist die Datei nach dem Schließen sofort frei (Umbenennen, Ersetzen, Löschen).</remarks>
    /// <param name="datei">Pfad der Datenbankdatei.</param>
    /// <param name="modus">Zugriffsart, z. B. nur lesen.</param>
    /// <returns>Der Verbindungstext.</returns>
    public static string Verbindungstext(string datei, SqliteOpenMode modus) =>
        new SqliteConnectionStringBuilder { DataSource = datei, Mode = modus, Pooling = false }.ToString();

    /// <summary>
    /// Entfernt eine Zwischendatei, falls vorhanden; Fehler dabei werden ignoriert.
    /// </summary>
    /// <remarks>
    /// Der eigentliche Fehler des Aufrufers ist wichtiger; eine Zwischendatei gilt nie als Sicherung und wird beim
    /// nächsten Versuch ersetzt.
    /// </remarks>
    /// <param name="zwischendatei">Pfad der Zwischendatei.</param>
    public static void LoescheZwischendatei(string zwischendatei)
    {
        try
        {
            File.Delete(zwischendatei);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            // Siehe oben: nicht kritisch
        }
    }
}
