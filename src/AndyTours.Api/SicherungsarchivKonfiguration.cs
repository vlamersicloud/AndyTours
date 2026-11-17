namespace AndyTours.Api;

using System.Globalization;
using AndyTours.Application.Sicherung;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Reads explicit server-side archive limits. Missing or invalid values disable only the feature;
/// the caller must not expose parser details and should return a fixed service-unavailable response.
/// </summary>
public static class SicherungsarchivKonfiguration
{
    private const string Prefix = "Backend:Sicherung:";

    public static bool TryLesen(IConfiguration configuration, out SicherungsarchivGrenzen? grenzen)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        grenzen = null;

        if (!TryPositiveInt(configuration[Prefix + "MaxEintraege"], out var maxEntries) ||
            maxEntries >= int.MaxValue ||
            !TryPositiveLong(configuration[Prefix + "MaxDateiBytes"], out var maxFileBytes) ||
            !TryPositiveLong(configuration[Prefix + "MaxGesamtBytes"], out var maxTotalBytes) ||
            !TryPositiveInt(configuration[Prefix + "MaxPfadzeichen"], out var maxPathChars) ||
            !TryPositiveLong(configuration[Prefix + "MaxArchivBytes"], out var maxArchiveBytes) ||
            !TryPositiveLong(configuration[Prefix + "MaxManifestBytes"], out var maxManifestBytes) ||
            !TryPositiveLong(configuration[Prefix + "MaxZentralverzeichnisBytes"], out var maxCentralDirectoryBytes))
            return false;

        if (maxFileBytes > maxTotalBytes ||
            maxManifestBytes > Array.MaxLength ||
            maxManifestBytes > maxArchiveBytes ||
            maxCentralDirectoryBytes > maxArchiveBytes)
            return false;

        grenzen = new SicherungsarchivGrenzen(
            new SicherungsmanifestGrenzen(maxEntries, maxFileBytes, maxTotalBytes, maxPathChars),
            maxArchiveBytes, maxManifestBytes, maxCentralDirectoryBytes);
        return true;
    }

    private static bool TryPositiveInt(string? value, out int parsed) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) && parsed > 0;

    private static bool TryPositiveLong(string? value, out long parsed) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) && parsed > 0;
}
