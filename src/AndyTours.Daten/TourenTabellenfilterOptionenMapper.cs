using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.Daten;

/// <summary>Validiert und kopiert Core-Filteroptionen in den HTTP-Vertrag.</summary>
public static class TourenTabellenfilterOptionenMapper
{
    private const string Fehlertext = "Tabellenfilteroptionen sind ungültig.";

    public static TourenTabellenfilterOptionenAntwort ToAntwort(Filterwerte raw)
    {
        if (raw is null || !IstGueltig(raw.Equipments) || !IstGueltig(raw.Trainingsbereiche))
            throw new UngueltigeTourException(Fehlertext);

        return new TourenTabellenfilterOptionenAntwort(
            Array.AsReadOnly(raw.Equipments.Skip(1).ToArray()),
            Array.AsReadOnly(raw.Trainingsbereiche.Skip(1).ToArray()));
    }

    private static bool IstGueltig(IReadOnlyList<string>? werte) =>
        werte is { Count: > 0 } &&
        string.Equals(werte[0], Filterwerte.Alle, StringComparison.Ordinal) &&
        werte.Skip(1).All(wert => !string.IsNullOrWhiteSpace(wert)) &&
        werte.Skip(1).Distinct(StringComparer.Ordinal).Count() == werte.Count - 1;
}
