namespace AndyTours.Core.Berechnungen;

/// <summary>
/// Macht aus einem Namen einen zulässigen Windows-Dateinamen (ohne Endung).
/// </summary>
public static class BereinigeDateiname
{
    /// <summary>Zeichen, die Windows in Dateinamen nicht erlaubt.</summary>
    private const string Unzulaessig = "\\/:*?\"<>|";

    /// <summary>
    /// Entfernt Leerzeichen am Rand und ersetzt unzulässige Zeichen (z. B. „/“) sowie Steuerzeichen durch „-“.
    /// </summary>
    /// <param name="name">Der Name, z. B. „Hin/zurück“.</param>
    /// <returns>Der bereinigte Name, z. B. „Hin-zurück“.</returns>
    public static string Ausfuehren(string name) =>
        new([.. name.Trim().Select(zeichen => Unzulaessig.Contains(zeichen) || char.IsControl(zeichen) ? '-' : zeichen)]);
}
