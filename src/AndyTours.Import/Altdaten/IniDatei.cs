namespace AndyTours.Import.Altdaten;

/// <summary>
/// Inhalt einer INI-Datei, gegliedert nach Abschnitten.
/// </summary>
/// <remarks>
/// Je Abschnitt werden die Zeilen unverändert als Text abgelegt (z. B. <c>weight=93</c>).
/// Schlüssel-Wert-Paare liefert <see cref="HoleIniSchluesselwerte"/>.
/// </remarks>
public class IniDatei
{
    /// <summary>
    /// Zeilen je Abschnitt; der Abschnittsname steht ohne eckige Klammern, Groß-/Kleinschreibung
    /// wird beim Nachschlagen ignoriert.
    /// </summary>
    public Dictionary<string, List<string>> Abschnitte { get; } = new(StringComparer.OrdinalIgnoreCase);
}
