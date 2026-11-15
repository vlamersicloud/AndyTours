using AndyTours.Daten.Datenordner;

namespace AndyTours.Altdatenuebernahme;

/// <summary>
/// Wertet die Befehlszeile des Programms aus.
/// </summary>
public static class LeseAufrufparameter
{
    /// <summary>Name des Ordners mit den BikeXperience-Altdaten im Projektordner.</summary>
    public const string StandardQuellordner = "Daten";

    /// <summary>
    /// Ermittelt Quell- und Zielordner aus den Argumenten.
    /// </summary>
    /// <remarks>
    /// Aufruf: <c>AndyTours.Altdatenuebernahme [BikeXperience-Ordner] [Zielordner]</c>.
    /// Ohne Zielordner: <c>AppDaten</c> im Projektordner (der Entwicklungs-Datenordner von AndyTours);
    /// ohne beide Angaben zusätzlich <c>Daten</c> im Projektordner als BikeXperience-Ordner.
    /// Den Projektordner findet das Programm wie die App über die Projektmappe oberhalb des Programmordners.
    /// Schalter (Angaben, die mit „--“ beginnen) gibt es nicht; sie werden abgelehnt, damit ein Tippfehler nicht
    /// als Ordner verstanden wird.
    /// </remarks>
    /// <param name="argumente">Die Befehlszeilenargumente.</param>
    /// <param name="programmordner">Ordner des laufenden Programms; Ausgangspunkt für die Suche nach dem Projektordner.</param>
    /// <returns>Die vollständigen Pfade der Ordner.</returns>
    /// <exception cref="ArgumentException">Zu viele Angaben oder ein Schalter.</exception>
    /// <exception cref="InvalidOperationException">Eine Vorgabe wird gebraucht, aber kein Projektordner gefunden.</exception>
    public static Aufrufparameter Ausfuehren(IReadOnlyList<string> argumente, string programmordner)
    {
        if (argumente.FirstOrDefault(a => a.StartsWith("--", StringComparison.Ordinal)) is string unbekannt)
        {
            throw new ArgumentException($"Unbekannter Schalter {unbekannt}.");
        }

        List<string> uebrige = [.. argumente];
        if (uebrige.Count > 2)
        {
            throw new ArgumentException("Zu viele Angaben. Erlaubt sind höchstens BikeXperience-Ordner und Zielordner.");
        }

        // Projektordner nur suchen, wenn eine Vorgabe gebraucht wird
        string Projektordner() => FindeProjektordner.Ausfuehren(programmordner)
            ?? throw new InvalidOperationException(
                $"Kein Projektordner gefunden (keine {DatenordnerInhalt.Projektmappe} oberhalb von {programmordner}). " +
                "Bitte BikeXperience-Ordner und Zielordner angeben.");

        string quelle = uebrige.Count >= 1 ? uebrige[0] : Path.Combine(Projektordner(), StandardQuellordner);
        string ziel = uebrige.Count >= 2 ? uebrige[1] : Path.Combine(Projektordner(), DatenordnerInhalt.Entwicklungsordner);

        // Vollständige Pfade, damit Meldungen eindeutig sind und relative Angaben nicht vom aktuellen Ordner abhängen
        return new Aufrufparameter(Path.GetFullPath(quelle), Path.GetFullPath(ziel));
    }
}
