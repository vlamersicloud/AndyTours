using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>
/// Liest und schreibt den Ordner, der zuletzt für „Als GPX exportieren…“ verwendet wurde.
/// </summary>
public static class GpxOrdnerEinstellung
{
    /// <summary>
    /// Liest den zuletzt verwendeten Ordner aus der Tabelle <c>Einstellung</c>.
    /// </summary>
    /// <remarks>
    /// Ob es den Ordner noch gibt, prüft der Aufrufer; sonst schlägt er „Dokumente“ vor.
    /// </remarks>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <returns>Der Ordner oder <c>null</c>, wenn noch keiner gespeichert ist.</returns>
    public static string? Laden(SqliteConnection verbindung)
    {
        string? wert = EinstellungTabelle.Lesen(verbindung, Einstellungsschluessel.GpxOrdner);
        return wert is string ordner && ordner.Length > 0 ? ordner : null;
    }

    /// <summary>
    /// Schreibt den verwendeten Ordner in die Tabelle <c>Einstellung</c>.
    /// </summary>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <param name="ordner">Der Ordner, in den zuletzt exportiert wurde.</param>
    public static void Speichern(SqliteConnection verbindung, string ordner)
    {
        EinstellungTabelle.Schreiben(verbindung, null, Einstellungsschluessel.GpxOrdner, ordner);
    }
}
