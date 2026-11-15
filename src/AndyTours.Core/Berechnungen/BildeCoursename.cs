using AndyTours.Core.Modell;

namespace AndyTours.Core.Berechnungen;

/// <summary>
/// Schlägt den Namen eines Course vor und bildet daraus den Dateinamen auf dem Garmin (Spec 032).
/// </summary>
public static class BildeCoursename
{
    /// <summary>
    /// Vorschlag für eine Planung: der Dateiname ohne Endung, bei einer ungespeicherten Planung „Planung JJJJ-MM-TT“.
    /// </summary>
    /// <param name="dateipfad">Pfad der gespeicherten bzw. geöffneten Planung oder <c>null</c>.</param>
    /// <param name="heute">Das heutige Datum.</param>
    /// <returns>Der Name.</returns>
    public static string FuerPlanung(string? dateipfad, DateOnly heute) =>
        Path.GetFileNameWithoutExtension(dateipfad ?? BildePlanungsdateiname.Ausfuehren(heute));

    /// <summary>
    /// Vorschlag für eine Etappe: Datum und Name wie beim GPX-Export, z. B. „2026-05-01 Bründlingalm“.
    /// </summary>
    /// <param name="etappe">Die Etappe.</param>
    /// <param name="bearbeitet"><c>true</c>, wenn der Track bearbeitet wurde (Zusatz „(bearbeitet)“).</param>
    /// <returns>Der Name.</returns>
    public static string FuerEtappe(Etappe etappe, bool bearbeitet = false) =>
        Path.GetFileNameWithoutExtension(BildeGpxDateiname.Ausfuehren([etappe], bearbeitet));

    /// <summary>
    /// Der Dateiname eines Course auf dem Garmin: der Name mit der Endung „.fit“.
    /// </summary>
    /// <remarks>
    /// Unzulässige Zeichen ersetzt <see cref="BereinigeDateiname"/>; Leerzeichen und Punkte am Ende entfallen,
    /// weil Windows sie nicht zulässt.
    /// </remarks>
    /// <param name="name">Name des Course; nicht leer.</param>
    /// <returns>Der Dateiname, z. B. „2026-05-01 Bründlingalm.fit“.</returns>
    public static string Dateiname(string name) =>
        BereinigeDateiname.Ausfuehren(name).TrimEnd(' ', '.') + ".fit";
}
