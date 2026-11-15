namespace AndyTours.Core.Berechnungen;

/// <summary>
/// Bildet die Anfangsliste einer Auswahlliste aus den Namen, die im Datenbestand vorkommen.
/// </summary>
/// <remarks>
/// Verwendet für die Listen „Trainingsbereich“ (Namen aus den Einheiten) und „Tätigkeit“ (Namen aus den
/// Wartungen), solange die Liste noch nie angelegt wurde (erster Start mit der Funktion, ältere Sicherung,
/// anderer Datenordner). Danach pflegt der Benutzer die Liste selbst.
/// </remarks>
public static class ErmittleNamensliste
{
    /// <summary>
    /// Liefert die vorkommenden Namen: ohne Leerzeichen am Rand, ohne leere Werte, ohne Dubletten
    /// (ohne Unterschied zwischen Groß- und Kleinschreibung; die zuerst gefundene Schreibweise gilt),
    /// alphabetisch geordnet.
    /// </summary>
    /// <param name="namen">Die Namen, z. B. die Trainingsbereiche aller Einheiten.</param>
    /// <returns>Die Anfangsliste, z. B. „GA1“, „GA2“, „K0“.</returns>
    public static List<string> Ausfuehren(IEnumerable<string> namen) => namen
        .Select(n => n.Trim())
        .Where(n => n.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.CurrentCultureIgnoreCase)
        .ToList();
}
