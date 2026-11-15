using System.IO;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using AndyTours.Import.Garmin;

namespace AndyTours.App.Funktionen;

/// <summary>Liest FIT-Dateien sequenziell im Hintergrund und behält nur kleine aggregierte Pulszeiten im Speicher.</summary>
public static class LesePulsstatistik
{
    /// <summary>Keine Schätzung aus Durchschnittspuls; fehlende oder unlesbare Aufzeichnungen werden gezählt.</summary>
    public static (Dictionary<(DateOnly Periode, int Bereich), double> Zeiten, int OhneDaten, double Fahrzeit)
        Ausfuehren(IReadOnlyList<Trainingseinheit> einheiten, string ordner, Zeitraster raster,
        GarminPulsprofil? profil, IProgress<string> fortschritt)
    {
        var zeiten = new Dictionary<(DateOnly, int), double>();
        int ohne = 0;
        string wurzel = Path.GetFullPath(ordner).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        for (int i = 0; i < einheiten.Count; i++)
        {
            Trainingseinheit e = einheiten[i];
            fortschritt.Report($"FIT-Aufzeichnungen lesen: {i + 1} von {einheiten.Count}");
            string? relativ = FindeAufzeichnung.Ausfuehren(e);
            if (relativ is null) { ohne++; continue; }
            string pfad;
            try { pfad = Path.GetFullPath(Path.Combine(wurzel, relativ)); }
            catch (Exception f) when (f is ArgumentException or NotSupportedException) { ohne++; continue; }
            if (!pfad.StartsWith(wurzel, StringComparison.OrdinalIgnoreCase) || !pfad.EndsWith(".fit", StringComparison.OrdinalIgnoreCase))
            { ohne++; continue; }
            (List<Verlaufspunkt>? punkte, _) = LeseFitVerlauf.Ausfuehren(pfad);
            if (punkte is null) { ohne++; continue; }
            IReadOnlyDictionary<int, double> bereiche = BerechnePulszeiten.Ausfuehren(punkte, profil);
            if (bereiche.Count == 0) { ohne++; }
            DateOnly periode = BerechneAktivitaetsperioden.Beginn(e.Datum, raster);
            foreach ((int bereich, double sekunden) in bereiche)
            {
                var key = (periode, bereich);
                zeiten[key] = zeiten.GetValueOrDefault(key) + sekunden;
            }
        }

        return (zeiten, ohne, einheiten.Sum(e => e.Fahrzeit.TotalSeconds));
    }
}
