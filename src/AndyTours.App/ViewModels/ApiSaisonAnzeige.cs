namespace AndyTours.App.ViewModels;

using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;

public sealed record ApiSaisonTexte(string Kilometer, string Hoehenmeter, string Anzahl, string Fahrzeit)
{
    public static ApiSaisonTexte Leer { get; } = new("", "", "", "");
}

public sealed class ApiSaisonAntwortException : Exception
{
    public ApiSaisonAntwortException() : base("Saisonkennzahlen sind unvollständig oder ungültig.") { }
}

public static class ApiSaisonAnzeige
{
    public static ApiSaisonTexte Erstellen(SaisonfortschrittAntwort? value, DateOnly von, DateOnly bis)
    {
        if (value is null) return ApiSaisonTexte.Leer;
        if (von != new DateOnly(value.Stichtag.Year, 1, 1) || bis != value.Stichtag ||
            value.Kilometer is null || value.Hoehenmeter is null || value.Anzahl is null || value.FahrzeitStunden is null)
            throw new ApiSaisonAntwortException();

        return new(
            Format(value.Kilometer, value.Stichtag.Year, Fortschrittswert.Kilometer),
            Format(value.Hoehenmeter, value.Stichtag.Year, Fortschrittswert.Hoehenmeter),
            Format(value.Anzahl, value.Stichtag.Year, Fortschrittswert.Anzahl),
            Format(value.FahrzeitStunden, value.Stichtag.Year, Fortschrittswert.Fahrzeit));
    }

    private static string Format(SaisonkennzahlAntwort k, int jahr, Fortschrittswert fortschrittswert)
    {
        var zeit = fortschrittswert == Fortschrittswert.Fahrzeit;
        var zahlen = new[] { k.Stand, k.Ziel, k.Anteil, k.Soll, k.Abweichung, k.Rest, k.ProWoche };
        if (zahlen.Any(x => x is { } n && !double.IsFinite(n)) ||
            (k.Ziel is null && (k.Anteil is not null || k.Soll is not null || k.Abweichung is not null || k.Rest is not null || k.ProWoche is not null)) ||
            (k.Ziel is not null && (k.Anteil is null || k.Soll is null || k.Abweichung is null || k.Rest is null)) ||
            (zeit && (k.Ziel is not null || k.Anteil is not null || k.Soll is not null || k.Abweichung is not null || k.Rest is not null || k.ProWoche is not null ||
                      k.Stand < TimeSpan.MinValue.TotalHours || k.Stand > TimeSpan.MaxValue.TotalHours)))
            throw new ApiSaisonAntwortException();

        var wert = new Jahreskennzahlen(jahr, k.Stand, k.Ziel, k.Anteil, k.Soll, k.Abweichung, k.Rest, k.ProWoche);
        var text = BeschreibeJahresfortschritt.Kennzahlen(new Jahresfortschritt(fortschrittswert, [], wert));
        return text.Ausblick.Length == 0 ? $"Saison: {text.Stand}" : $"Saison: {text.Stand} · {text.Ausblick}";
    }
}
