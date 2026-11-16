using System.Text;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Application.Overlays;

/// <summary>Pure request/name rules shared by overlay GPX import layers.</summary>
public static class OverlayGpxRegeln
{
    public const long MaxDateiBytes = 134217728;
    private const string Fehlermeldung = "Overlay-GPX-Anfrage ungültig.";

    public static OverlayGpxImportAnfrage Normalisieren(OverlayGpxImportAnfrage anfrage)
    {
        if (anfrage is null || anfrage.Version is not { Length: 64 } ||
            !anfrage.Version.All(Uri.IsHexDigit) || anfrage.AnfrageId == Guid.Empty ||
            anfrage.OrdnerId is <= 0)
            throw Invalid();

        string dateiname = NormalisiereDateiname(anfrage.Dateiname);
        _ = AnzeigenameAusBasename(dateiname);
        return anfrage with { Version = anfrage.Version.ToUpperInvariant(), Dateiname = dateiname };
    }

    public static string Anzeigename(string dateiname) =>
        AnzeigenameAusBasename(NormalisiereDateiname(dateiname));

    private static string NormalisiereDateiname(string dateiname)
    {
        if (string.IsNullOrWhiteSpace(dateiname)) throw Invalid();
        string normalized;
        try { normalized = dateiname.Normalize(NormalizationForm.FormC); }
        catch (ArgumentException) { throw Invalid(); }

        if (normalized.Length is < 1 or > 255 || normalized is "." or ".." ||
            normalized.IndexOfAny(['/', '\\']) >= 0 || normalized.Any(char.IsControl))
            throw Invalid();
        return normalized;
    }

    private static string AnzeigenameAusBasename(string basename)
    {
        string displayName;
        try { displayName = Path.GetFileNameWithoutExtension(basename).Trim().Normalize(NormalizationForm.FormC); }
        catch (ArgumentException) { throw Invalid(); }
        if (displayName.Length == 0 || displayName.Any(char.IsControl)) throw Invalid();
        return displayName;
    }

    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, Fehlermeldung);
}
