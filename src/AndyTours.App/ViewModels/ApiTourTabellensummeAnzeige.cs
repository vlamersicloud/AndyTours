using System.Globalization;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

/// <summary>Erzeugt die Anzeige aus serverseitig berechneten Summen für alle Filtertreffer.</summary>
public static class ApiTourTabellensummeAnzeige
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");
    private const string Fehlend = "Tabellensumme nicht verfügbar – Backend-Version prüfen.";

    public static string Erstellen(TourListeAntwort page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var sum = page.Tabellensumme;
        if (sum is null) return Fehlend;
        if (sum.Anzahl < 0 || sum.Anzahl != page.Gesamt || !double.IsFinite(sum.DistanzKm) ||
            (page.Gesamt == 0 && (sum.DistanzKm != 0 || sum.Hoehenmeter != 0 || sum.FahrzeitSekunden != 0)))
            throw new ApiTourSeitenException();

        var hours = sum.FahrzeitSekunden / 3600;
        var minutes = sum.FahrzeitSekunden / 60 % 60;
        var seconds = sum.FahrzeitSekunden % 60;
        return string.Format(Deutsch, "Alle Filtertreffer: {0:N0} Touren · {1:N2} km · {2:N0} hm · {3}:{4:00}:{5:00}",
            sum.Anzahl, sum.DistanzKm, sum.Hoehenmeter, hours, minutes, seconds);
    }
}
