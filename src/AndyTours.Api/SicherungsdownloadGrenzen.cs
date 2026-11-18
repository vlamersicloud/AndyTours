namespace AndyTours.Api;

using System.Globalization;

/// <summary>Parses the mandatory, fail-closed server download byte limit.</summary>
internal static class SicherungsdownloadGrenzen
{
    internal static bool TryParse(string? value, out long maximum) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out maximum) && maximum > 0;
}
