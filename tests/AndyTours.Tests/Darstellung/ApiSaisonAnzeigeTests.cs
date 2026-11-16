namespace AndyTours.Tests.Darstellung;

using System.Globalization;
using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;
using Xunit;

public sealed class ApiSaisonAnzeigeTests
{
    [Fact]
    public void FormatiertVierKennzahlenMitDeutscherKultur()
    {
        var vorher = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var value = new SaisonfortschrittAntwort(new DateOnly(2024, 2, 29),
                new(12.5, null, null, null, null, null, null),
                new(200, null, null, null, null, null, null),
                new(3, null, null, null, null, null, null),
                new(25.5, null, null, null, null, null, null));
            var result = ApiSaisonAnzeige.Erstellen(value, new(2024, 1, 1), new(2024, 2, 29));
            Assert.Equal("Saison: 2024: 12,5 km · kein Saisonziel – unter Extras → Saisonziele… festlegen", result.Kilometer);
            Assert.Equal("Saison: 2024: 200 hm · kein Saisonziel – unter Extras → Saisonziele… festlegen", result.Hoehenmeter);
            Assert.Equal("Saison: 2024: 3 Einheiten · kein Saisonziel – unter Extras → Saisonziele… festlegen", result.Anzahl);
            Assert.Equal("Saison: 2024: 25:30 h", result.Fahrzeit);
        }
        finally { CultureInfo.CurrentCulture = vorher; }
    }

    [Fact]
    public void FormatiertZielUndFehlendeProWoche()
    {
        var value = new SaisonfortschrittAntwort(new(2024, 2, 29),
            new(50, 100, .5, 25, 25, 50, 10),
            new(0, null, null, null, null, null, null),
            new(0, null, null, null, null, null, null),
            new(0, null, null, null, null, null, null));
        var result = ApiSaisonAnzeige.Erstellen(value, new(2024, 1, 1), new(2024, 2, 29));
        Assert.Equal("Saison: 2024: 50,0 km von 100 km (50 %) · Soll heute 25,0 km · 25,0 km Vorsprung · noch 50,0 km – 10,0 km pro Woche", result.Kilometer);
    }

    [Fact]
    public void FehlendeOptionaleProjektionBleibtLeer() =>
        Assert.Equal(ApiSaisonTexte.Leer, ApiSaisonAnzeige.Erstellen(null, new(2024, 2, 1), new(2024, 2, 29)));

    [Theory]
    [MemberData(nameof(UngueltigeAntworten))]
    public void VerwirftUngueltigesProtokoll(SaisonfortschrittAntwort value, DateOnly von, DateOnly bis) =>
        Assert.Equal("Saisonkennzahlen sind unvollständig oder ungültig.",
            Assert.Throws<ApiSaisonAntwortException>(() => ApiSaisonAnzeige.Erstellen(value, von, bis)).Message);

    public static IEnumerable<object[]> UngueltigeAntworten()
    {
        var datum = new DateOnly(2024, 2, 29);
        var leer = new SaisonkennzahlAntwort(1, null, null, null, null, null, null);
        var basis = new SaisonfortschrittAntwort(datum, leer, leer, leer, leer);
        yield return [basis, new DateOnly(2024, 1, 2), datum];
        yield return [basis with { Kilometer = null! }, new DateOnly(2024, 1, 1), datum];
        yield return [basis with { Kilometer = leer with { Stand = double.NaN } }, new DateOnly(2024, 1, 1), datum];
        yield return [basis with { Kilometer = leer with { Stand = double.PositiveInfinity } }, new DateOnly(2024, 1, 1), datum];
        yield return [basis with { Kilometer = leer with { Ziel = 10, Anteil = .5 } }, new DateOnly(2024, 1, 1), datum];
        yield return [basis with { Kilometer = leer with { Anteil = .5 } }, new DateOnly(2024, 1, 1), datum];
        yield return [basis with { FahrzeitStunden = leer with { Ziel = 10, Anteil = .5, Soll = 1, Abweichung = 1, Rest = 1 } }, new DateOnly(2024, 1, 1), datum];
        yield return [basis with { FahrzeitStunden = leer with { Stand = 1e100 } }, new DateOnly(2024, 1, 1), datum];
    }
}
