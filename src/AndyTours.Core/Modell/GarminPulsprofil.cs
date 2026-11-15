namespace AndyTours.Core.Modell;

/// <summary>Von Garmin gelieferte aktuelle bpm-Untergrenzen; keine historischen oder geschätzten Grenzen.</summary>
public sealed record GarminPulsprofil(string Sport, IReadOnlyList<int> Untergrenzen)
{
    /// <summary>Expliziter Anzeigename des Sportprofils.</summary>
    public string Anzeige => Sport switch
    {
        "CYCLING" => "Radfahren (CYCLING)", "RUNNING" => "Laufen (RUNNING)",
        "DEFAULT" => "Allgemein (DEFAULT)", "SWIMMING" => "Schwimmen (SWIMMING)",
        "ROWING" => "Rudern (ROWING)", _ => Sport,
    };

    /// <summary>Zone 0 heißt unterhalb Zone 1, Zone 5 bleibt nach oben offen (Maximalpuls ist keine harte Messgrenze).</summary>
    public int Zone(int bpm)
    {
        for (int i = Untergrenzen.Count - 1; i >= 0; i--)
        {
            if (bpm >= Untergrenzen[i]) { return i + 1; }
        }

        return 0;
    }

    /// <summary>Konkreter bpm-Bereich ohne überlappende Grenzen.</summary>
    public string Bereich(int zone) => zone == 0 ? $"unter {Untergrenzen[0]} bpm"
        : zone == 5 ? $"ab {Untergrenzen[4]} bpm"
        : $"{Untergrenzen[zone - 1]}–{Untergrenzen[zone] - 1} bpm";
}
