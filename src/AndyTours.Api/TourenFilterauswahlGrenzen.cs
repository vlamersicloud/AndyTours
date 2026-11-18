using System.Globalization;

namespace AndyTours.Api;

/// <summary>Reine fail-closed Auswertung der beiden verpflichtenden Filterauswahlgrenzen.</summary>
internal static class TourenFilterauswahlGrenzen
{
    internal static bool TryParse(
        string? maxTourenText, string? maxAntwortBytesText,
        out int maxTouren, out int maxAntwortBytes)
    {
        maxTouren = 0;
        maxAntwortBytes = 0;
        return int.TryParse(maxTourenText, NumberStyles.None, CultureInfo.InvariantCulture, out maxTouren) &&
               maxTouren > 0 &&
               int.TryParse(maxAntwortBytesText, NumberStyles.None, CultureInfo.InvariantCulture, out maxAntwortBytes) &&
               maxAntwortBytes > 0;
    }
}
