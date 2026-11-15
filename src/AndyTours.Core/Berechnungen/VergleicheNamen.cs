namespace AndyTours.Core.Berechnungen;

/// <summary>
/// Vergleicht zwei Namen so, wie AndyTours Einträge von Auswahllisten vergleicht.
/// </summary>
/// <remarks>
/// Kategorien, Tätigkeiten, Trainingsbereiche, Ordner usw. gelten als gleich, wenn sie sich nur in Groß- und
/// Kleinschreibung oder in Leerzeichen am Rand unterscheiden („ Kette “ = „kette“).
/// </remarks>
public static class VergleicheNamen
{
    /// <summary>
    /// Liefert <c>true</c>, wenn beide Namen ohne Leerzeichen am Rand und ohne Unterschied zwischen Groß- und
    /// Kleinschreibung gleich sind.
    /// </summary>
    /// <param name="a">Erster Name.</param>
    /// <param name="b">Zweiter Name.</param>
    /// <returns><c>true</c> bei gleichen Namen.</returns>
    public static bool Ausfuehren(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
