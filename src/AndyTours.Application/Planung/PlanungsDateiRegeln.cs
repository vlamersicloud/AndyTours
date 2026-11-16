using System.Text;
using AndyTours.Application.Stammdaten;

namespace AndyTours.Application.Planung;

/// <summary>Shared bounded filename rules for planning GPX and Course files.</summary>
public static class PlanungsDateiRegeln
{
    public const long MaxDateiBytes = 134_217_728;

    public static string NormalisiereName(string name)
    {
        string normalized;
        try { normalized = (name ?? string.Empty).Trim().Normalize(NormalizationForm.FormC); }
        catch (ArgumentException) { throw Invalid(); }
        if (normalized.Length is 0 or > 200 || normalized is "." or ".." || normalized.Contains("..", StringComparison.Ordinal) ||
            normalized.Any(char.IsControl) || normalized.IndexOfAny(['/', '\\']) >= 0)
            throw Invalid();
        return normalized;
    }

    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, "Dateiname ungültig.");
}
