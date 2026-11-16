namespace AndyTours.Application.Sicherung;

using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

public static class SicherungskonfigurationRegeln
{
    public static void Pruefe(SicherungskonfigurationAnfrage? anfrage)
    {
        if (anfrage is null || anfrage.Version is null || anfrage.Version.Length != 64 ||
            !anfrage.Version.All(Uri.IsHexDigit) ||
            anfrage.Intervall is < Sicherungseinstellungen.Minimalwert or > Sicherungseinstellungen.Maximalwert ||
            anfrage.Hoechstzahl is < Sicherungseinstellungen.Minimalwert or > Sicherungseinstellungen.Maximalwert)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Sicherungskonfiguration ungültig.");
    }
}
