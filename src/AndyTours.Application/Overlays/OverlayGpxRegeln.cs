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

        string dateiname;
        try
        {
            if (string.IsNullOrWhiteSpace(anfrage.Dateiname)) throw Invalid();
            dateiname = anfrage.Dateiname.Normalize(NormalizationForm.FormC);
            if (dateiname.Length is < 1 or > 255 || dateiname is "." or ".." ||
                dateiname.IndexOfAny(['/', '\\']) >= 0 || dateiname.Any(char.IsControl))
                throw Invalid();
        }
        catch (ArgumentException)
        {
            throw Invalid();
        }

        return anfrage with { Version = anfrage.Version.ToUpperInvariant(), Dateiname = dateiname };
    }

    public static string Anzeigename(string dateiname)
    {
        var request = new OverlayGpxImportAnfrage(new string('0', 64), Guid.NewGuid(), null, dateiname);
        string basename = Normalisieren(request).Dateiname;
        string displayName = Path.GetFileNameWithoutExtension(basename).Trim().Normalize(NormalizationForm.FormC);
        if (displayName.Length == 0 || displayName.Any(char.IsControl)) throw Invalid();
        return displayName;
    }

    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, Fehlermeldung);
}
