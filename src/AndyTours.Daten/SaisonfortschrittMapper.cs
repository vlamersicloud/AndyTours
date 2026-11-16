using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.Daten;

/// <summary>Kopiert validierte Core-Jahreskennzahlen in den Saison-Wire-Vertrag.</summary>
public static class SaisonfortschrittMapper
{
    private const string Fehlertext = "Die Saisonkennzahlen sind ungültig.";

    public static SaisonfortschrittAntwort ToAntwort(
        DateOnly stichtag,
        Jahreskennzahlen kilometer,
        Jahreskennzahlen hoehenmeter,
        Jahreskennzahlen anzahl,
        Jahreskennzahlen fahrzeitStunden)
    {
        if (!Gueltig(kilometer, stichtag.Year) || !Gueltig(hoehenmeter, stichtag.Year) ||
            !Gueltig(anzahl, stichtag.Year) || !Gueltig(fahrzeitStunden, stichtag.Year) ||
            !ZieleNull(fahrzeitStunden))
            throw new UngueltigeTourException(Fehlertext);

        return new SaisonfortschrittAntwort(
            stichtag,
            Abbilden(kilometer),
            Abbilden(hoehenmeter),
            Abbilden(anzahl),
            Abbilden(fahrzeitStunden));
    }

    private static bool Gueltig(Jahreskennzahlen? value, int year) =>
        value is not null && value.Jahr == year && double.IsFinite(value.Stand) &&
        Endlich(value.Ziel) && Endlich(value.Anteil) && Endlich(value.Soll) &&
        Endlich(value.Differenz) && Endlich(value.Rest) && Endlich(value.ProWoche);

    private static bool Endlich(double? value) => value is null || double.IsFinite(value.Value);

    private static bool ZieleNull(Jahreskennzahlen value) =>
        value.Ziel is null && value.Anteil is null && value.Soll is null && value.Differenz is null &&
        value.Rest is null && value.ProWoche is null;

    private static SaisonkennzahlAntwort Abbilden(Jahreskennzahlen value) =>
        new(value.Stand, value.Ziel, value.Anteil, value.Soll, value.Differenz, value.Rest, value.ProWoche);
}
