using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Persönlicher Garmin-Key im aktuellen Datenbestand, analog zum vorhandenen Routing-Key.</summary>
/// <remarks>Der Schlüssel liegt unverschlüsselt in SQLite und damit auch in Sicherungen. Niemals protokollieren.</remarks>
public static class GarminEinstellung
{
    /// <summary>Liest den hinterlegten Key; ohne Einstellung wird kein Umgebungsvariablen-Fallback verwendet.</summary>
    public static string? Laden(SqliteConnection verbindung) =>
        EinstellungTabelle.Lesen(verbindung, Einstellungsschluessel.GarminPersonalApiKey);

    /// <summary>Speichert den bereinigten Key. Leer oder null entfernt die Einstellung vollständig.</summary>
    public static void Speichern(SqliteConnection verbindung, string? schluessel)
    {
        string? wert = string.IsNullOrWhiteSpace(schluessel) ? null : schluessel.Trim();
        EinstellungTabelle.Schreiben(verbindung, null, Einstellungsschluessel.GarminPersonalApiKey, wert);
    }
    /// <summary>Ohne Einstellung bleibt die bisherige manuelle Bestätigung zwischen Paketen aktiv.</summary>
    public static bool AutomatischLaden(SqliteConnection verbindung) =>
        EinstellungTabelle.Lesen(verbindung, Einstellungsschluessel.GarminAllePaketeAutomatisch) == "true";

    /// <summary>Speichert die automatische Folge der 20er-Pakete unabhängig vom persönlichen API-Key.</summary>
    public static void AutomatischSpeichern(SqliteConnection verbindung, bool automatisch) =>
        EinstellungTabelle.Schreiben(verbindung, null, Einstellungsschluessel.GarminAllePaketeAutomatisch,
            automatisch ? "true" : null);
}
