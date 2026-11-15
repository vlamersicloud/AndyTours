using System.Text.Json;
using AndyTours.Core.Modell;

namespace AndyTours.Import.Garmin;

/// <summary>Validiert HeartRateZonesDTO gemäß Garmins offiziellem Personal-API-OpenAPI-Schema.</summary>
public static class LeseGarminPulsprofile
{
    /// <summary>Liest sport und zone1Floor bis zone5Floor; keine Berechnung aus Alter oder Maximalpuls.</summary>
    public static IReadOnlyList<GarminPulsprofil> Ausfuehren(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Array) { throw new InvalidDataException("Garmin hat keine Pulszonenliste geliefert."); }
        var profile = new List<GarminPulsprofil>();
        foreach (JsonElement eintrag in json.EnumerateArray())
        {
            if (eintrag.ValueKind != JsonValueKind.Object || !eintrag.TryGetProperty("sport", out JsonElement sportfeld) || sportfeld.ValueKind != JsonValueKind.String)
            { throw new InvalidDataException("Garmin-Pulsprofil ohne Sportkennung."); }
            string sport = sportfeld.GetString()!;
            int[] grenzen = new int[5];
            for (int i = 0; i < 5; i++)
            {
                if (!eintrag.TryGetProperty($"zone{i + 1}Floor", out JsonElement feld)
                    || feld.ValueKind != JsonValueKind.Number || !feld.TryGetInt32(out grenzen[i]) || grenzen[i] <= 0 || grenzen[i] >= 255
                    || (i > 0 && grenzen[i] <= grenzen[i - 1]))
                { throw new InvalidDataException("Garmin hat unvollständige oder nicht aufsteigende bpm-Grenzen geliefert."); }
            }

            if (profile.Any(p => p.Sport == sport)) { throw new InvalidDataException("Garmin hat doppelte Pulsprofile geliefert."); }
            profile.Add(new GarminPulsprofil(sport, grenzen));
        }

        return profile;
    }
}
