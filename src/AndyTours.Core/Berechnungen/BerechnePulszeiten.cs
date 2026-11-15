using AndyTours.Core.Modell;

namespace AndyTours.Core.Berechnungen;

/// <summary>Zeitgewichtete Pulsbereiche aus echten Messwerten; keine Verteilung aus Tour-Durchschnittswerten.</summary>
public static class BerechnePulszeiten
{
    /// <summary>
    /// Ordnet das aktive Intervall zwischen zwei gültigen Messpunkten dem vorherigen Puls zu.
    /// Maximal 60 Sekunden Messabstand, keine Zeit nach dem letzten Punkt, keine fehlenden Werte interpolieren.
    /// Schlüssel: Garmin-Zone 0–5 oder ohne Profil untere Grenze eines 10-bpm-Bereichs.
    /// </summary>
    public static IReadOnlyDictionary<int, double> Ausfuehren(IReadOnlyList<Verlaufspunkt> verlauf, GarminPulsprofil? profil)
    {
        var sekunden = new Dictionary<int, double>();
        for (int i = 1; i < verlauf.Count; i++)
        {
            Verlaufspunkt vorher = verlauf[i - 1], jetzt = verlauf[i];
            double aktiv = (jetzt.Fahrzeit - vorher.Fahrzeit).TotalSeconds;
            double abstand = vorher.ZeitpunktUtc is DateTime a && jetzt.ZeitpunktUtc is DateTime b
                ? (b - a).TotalSeconds : aktiv;
            if (aktiv <= 0 || abstand <= 0 || abstand > 60 || aktiv > abstand
                || vorher.Puls is not (> 0 and < 255) || jetzt.Puls is not (> 0 and < 255)) { continue; }
            int puls = vorher.Puls.Value;
            int bereich = profil?.Zone(puls) ?? (puls / 10 * 10);
            sekunden[bereich] = sekunden.GetValueOrDefault(bereich) + aktiv;
        }

        return sekunden;
    }
}
