using System.Text;
using AndyTours.Application.Stammdaten;

namespace AndyTours.Application.Import;

/// <summary>Pure validation rules shared by FIT upload clients and server storage.</summary>
public static class FitUploadRegeln
{
    public const long MaxDateiBytes = 134217728;

    public static string Dateiname(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw Invalid();
        string normalized;
        try
        {
            normalized = value.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            throw Invalid();
        }

        if (normalized.Length is < 1 or > 255 || normalized is "." or ".." ||
            normalized.IndexOfAny(['/', '\\']) >= 0 || normalized.Any(char.IsControl))
            throw Invalid();
        return normalized;
    }

    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, "FIT-Dateiname ungültig.");
}
