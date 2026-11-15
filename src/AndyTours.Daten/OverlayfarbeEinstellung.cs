using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>
/// Liest und schreibt die gemeinsame Overlay-Farbe (Einstellungen, Gruppe „Overlays“).
/// </summary>
/// <remarks>
/// In dieser Farbe erscheinen alle Overlays, die keine eigene Farbe haben.
/// </remarks>
public static class OverlayfarbeEinstellung
{
    /// <summary>Die voreingestellte gemeinsame Overlay-Farbe.</summary>
    public const Overlayfarbe Standard = Overlayfarbe.Violett;

    /// <summary>
    /// Liest die gemeinsame Overlay-Farbe aus der Tabelle <c>Einstellung</c>.
    /// </summary>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <returns>Die Farbe; ohne (gültigen) Eintrag <see cref="Standard"/>.</returns>
    public static Overlayfarbe Laden(SqliteConnection verbindung)
    {
        string? wert = EinstellungTabelle.Lesen(verbindung, Einstellungsschluessel.OverlayFarbe);
        return wert is not null && Enum.TryParse(wert, out Overlayfarbe farbe) && Enum.IsDefined(farbe)
            ? farbe
            : Standard;
    }

    /// <summary>
    /// Schreibt die gemeinsame Overlay-Farbe in die Tabelle <c>Einstellung</c>.
    /// </summary>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <param name="farbe">Die Farbe.</param>
    public static void Speichern(SqliteConnection verbindung, Overlayfarbe farbe)
    {
        EinstellungTabelle.Schreiben(verbindung, null, Einstellungsschluessel.OverlayFarbe, farbe.ToString());
    }
}
