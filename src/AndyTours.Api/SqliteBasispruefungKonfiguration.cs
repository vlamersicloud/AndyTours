namespace AndyTours.Api;

using System.Globalization;
using AndyTours.Application.Sicherung;
using Microsoft.Extensions.Configuration;
using System.Security;

/// <summary>Explicit fail-closed server settings for the optional SQLite inspection endpoint.</summary>
public sealed record SqliteBasispruefungKonfiguration(string CheckerExecutable, string Pruefordner,
    int LaufzeitMillisekunden, int MaxOutputBytes, long MaxDatenbankBytes, SicherungsarchivGrenzen Archivgrenzen)
{
    public static bool TryLesen(IConfiguration configuration, string aktiverDatenordner,
        out SqliteBasispruefungKonfiguration? settings)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        settings = null;
        const string prefix = "Backend:Sicherung:SqlitePruefung:";
        var executable = configuration[prefix + "CheckerExecutable"];
        var stageRoot = configuration[prefix + "Pruefordner"];
        if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable) ||
            string.IsNullOrWhiteSpace(stageRoot) || !Path.IsPathFullyQualified(stageRoot) ||
            !Path.IsPathFullyQualified(aktiverDatenordner) ||
            !int.TryParse(configuration[prefix + "LaufzeitMillisekunden"], NumberStyles.None, CultureInfo.InvariantCulture, out var timeout) || timeout <= 0 ||
            !int.TryParse(configuration[prefix + "MaxOutputBytes"], NumberStyles.None, CultureInfo.InvariantCulture, out var output) || output <= 0 ||
            !long.TryParse(configuration[prefix + "MaxDatenbankBytes"], NumberStyles.None, CultureInfo.InvariantCulture, out var database) || database <= 0 ||
            !SicherungsarchivKonfiguration.TryLesen(configuration, out var archive) || archive is null)
            return false;
        try
        {
            var stage = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stageRoot));
            var active = Path.TrimEndingDirectorySeparator(Path.GetFullPath(aktiverDatenordner));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!Directory.Exists(stage) || !Directory.Exists(active) || !File.Exists(executable) ||
                HasReparseAncestor(stage) || HasReparseAncestor(active) || HasReparseAncestor(executable) ||
                Overlaps(stage, active, comparison)) return false;
            settings = new SqliteBasispruefungKonfiguration(Path.GetFullPath(executable), stage, timeout, output, database, archive);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or SecurityException)
        { return false; }
    }
    private static bool Overlaps(string a, string b, StringComparison comparison) =>
        SameOrUnder(a, b, comparison) || SameOrUnder(b, a, comparison);
    private static bool SameOrUnder(string candidate, string root, StringComparison comparison)
    {
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return string.Equals(candidate, root, comparison) || candidate.StartsWith(prefix, comparison);
    }
    private static bool HasReparseAncestor(string path)
    {
        for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current)!)
        {
            try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true; }
            catch { return true; }
            var parent = Path.GetDirectoryName(current);
            if (parent is null || parent == current) break;
        }
        return false;
    }
}
