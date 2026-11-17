using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Application.Tabellenlayout;

public static class EinheitentabellenlayoutRegeln
{
    private static readonly HashSet<Einheitentabellenschluessel> Defined = Enum.GetValues<Einheitentabellenschluessel>().ToHashSet();
    public static void Pruefe(EinheitentabellenlayoutAnfrage? a)
    {
        if (a is null || a.Version is null || a.Version.Length != 64 || !a.Version.All(Uri.IsHexDigit) || a.Anordnung is null || a.Ausgeblendet is null || a.Anordnung.Count != Defined.Count || a.Anordnung.Any(x => x is null) || a.Anordnung.Select(x => x.Schluessel).Distinct().Count() != Defined.Count || a.Anordnung.Any(x => x is null || !Defined.Contains(x.Schluessel) || x.Breite is double w && (!double.IsFinite(w) || w <= 0 || (double.IsFinite(Math.Round(w, 1)) && Math.Round(w, 1) <= 0))) || a.Ausgeblendet.Any(x => !Defined.Contains(x)) || a.Ausgeblendet.Distinct().Count() != a.Ausgeblendet.Count || a.Ausgeblendet.Count == Defined.Count)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Tabellenlayout ungültig.");
    }
}
