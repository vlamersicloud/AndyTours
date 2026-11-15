using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>
/// Liest und schreibt die ausgeblendeten Spalten der Tabelle „Trainingseinheiten“.
/// </summary>
public static class AusgeblendeteSpaltenEinstellung
{
    /// <summary>
    /// Liest die ausgeblendeten Spalten aus der Tabelle <c>Einstellung</c>.
    /// </summary>
    /// <remarks>
    /// Gespeichert sind die Namen der <see cref="Einheitenspalte"/>-Werte, durch Komma getrennt. Fehlt
    /// der Eintrag, sind alle Spalten sichtbar. Unbekannte Namen (z. B. von Hand verändert oder eine
    /// inzwischen entfallene Spalte) werden übergangen. Sind dadurch alle Spalten ausgeblendet, gilt
    /// die Wahl als unbrauchbar und alle Spalten sind wieder sichtbar.
    /// </remarks>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <returns>Die ausgeblendeten Spalten (leer = alle sichtbar).</returns>
    public static HashSet<Einheitenspalte> Laden(SqliteConnection verbindung)
    {
        string? wert = EinstellungTabelle.Lesen(verbindung, Einstellungsschluessel.AusgeblendeteSpalten);
        string text = wert ?? string.Empty;

        var ausgeblendet = new HashSet<Einheitenspalte>();
        foreach (string name in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // ignoreCase: false und Prüfung auf definierte Werte – Zahlen wie „3“ würden sonst auch akzeptiert
            if (Enum.TryParse(name, ignoreCase: false, out Einheitenspalte spalte) && Enum.IsDefined(spalte)
                && !int.TryParse(name, out _))
            {
                ausgeblendet.Add(spalte);
            }
        }

        return ausgeblendet.Count < Enum.GetValues<Einheitenspalte>().Length ? ausgeblendet : [];
    }

    /// <summary>
    /// Schreibt die ausgeblendeten Spalten in die Tabelle <c>Einstellung</c>.
    /// </summary>
    /// <remarks>
    /// Gespeichert werden die ausgeblendeten (nicht die sichtbaren) Spalten, damit Spalten, die in
    /// einer späteren Version hinzukommen, automatisch sichtbar sind. Die Namen stehen in
    /// Anzeigereihenfolge, durch Komma getrennt. Eine leere Menge löscht den Eintrag (= alle sichtbar).
    /// </remarks>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <param name="ausgeblendet">Die ausgeblendeten Spalten.</param>
    public static void Speichern(SqliteConnection verbindung, IReadOnlySet<Einheitenspalte> ausgeblendet)
    {
        EinstellungTabelle.Schreiben(verbindung, null, Einstellungsschluessel.AusgeblendeteSpalten,
            ausgeblendet.Count == 0 ? null : string.Join(",", ausgeblendet.Order().Select(s => s.ToString())));
    }
}
