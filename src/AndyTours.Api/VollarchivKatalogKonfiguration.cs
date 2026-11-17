namespace AndyTours.Api;

using System.Globalization;
using AndyTours.Application.Sicherung;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Reads the explicit server-side creation and full-catalog bounds. There are no defaults; missing or malformed
/// catalog limits disable only full-catalog requests, not application startup or legacy-only listing.
/// </summary>
public static class VollarchivKatalogKonfiguration
{
    private const string Prefix = "Backend:Sicherung:";

    public static bool TryLesen(IConfiguration configuration, out VollarchivKatalogGrenzen? grenzen)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        grenzen = null;

        if (!SicherungsarchivKonfiguration.TryLesen(configuration, out var archive) ||
            !TryPositiveInt(configuration[Prefix + "MaxKatalogVollarchive"], out var maxArchives) ||
            !TryPositiveLong(configuration[Prefix + "MaxKatalogNutzdatenBytes"], out var maxPayloadBytes))
            return false;

        grenzen = new VollarchivKatalogGrenzen(archive!, maxArchives, maxPayloadBytes);
        return true;
    }

    private static bool TryPositiveInt(string? value, out int parsed) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) && parsed > 0;

    private static bool TryPositiveLong(string? value, out long parsed) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) && parsed > 0;
}
