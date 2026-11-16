using System.Globalization;
using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiTourTabellensummeAnzeigeTests
{
    [Fact]
    public void Formats_server_total_using_german_culture_and_all_filtered_scope()
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var page = new TourListeAntwort([], 1234, 0, 50)
            {
                Tabellensumme = new TourenTabellensummeAntwort(1234, 1234.56, 7890, 90061)
            };
            Assert.Equal("Alle Filtertreffer: 1.234 Touren · 1.234,56 km · 7.890 hm · 25:01:01",
                ApiTourTabellensummeAnzeige.Erstellen(page));
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    [Fact]
    public void Formats_long_max_duration_without_timespan_overflow()
    {
        var page = new TourListeAntwort([], 1, 0, 50)
        {
            Tabellensumme = new TourenTabellensummeAntwort(1, 0, 0, long.MaxValue)
        };
        Assert.Equal("Alle Filtertreffer: 1 Touren · 0,00 km · 0 hm · 2562047788015215:30:07",
            ApiTourTabellensummeAnzeige.Erstellen(page));
    }

    [Fact]
    public void Zero_total_is_distinct_from_missing_metadata()
    {
        var zero = new TourListeAntwort([], 0, 0, 50) { Tabellensumme = new TourenTabellensummeAntwort(0, 0, 0, 0) };
        Assert.Equal("Alle Filtertreffer: 0 Touren · 0,00 km · 0 hm · 0:00:00", ApiTourTabellensummeAnzeige.Erstellen(zero));
        var missing = new TourListeAntwort([], 0, 0, 50);
        Assert.Equal("Tabellensumme nicht verfügbar – Backend-Version prüfen.", ApiTourTabellensummeAnzeige.Erstellen(missing));
    }

    [Theory]
    [MemberData(nameof(InvalidTotals))]
    public void Rejects_inconsistent_or_nonfinite_totals(TourListeAntwort page)
    {
        Assert.Throws<ApiTourSeitenException>(() => ApiTourTabellensummeAnzeige.Erstellen(page));
    }

    public static IEnumerable<object?[]> InvalidTotals() =>
    [
        [new TourListeAntwort([], 0, 0, 50) { Tabellensumme = new TourenTabellensummeAntwort(-1, 0, 0, 0) }],
        [new TourListeAntwort([], 2, 0, 50) { Tabellensumme = new TourenTabellensummeAntwort(1, 0, 0, 0) }],
        [new TourListeAntwort([], 1, 0, 50) { Tabellensumme = new TourenTabellensummeAntwort(1, double.NaN, 0, 0) }],
        [new TourListeAntwort([], 1, 0, 50) { Tabellensumme = new TourenTabellensummeAntwort(1, double.PositiveInfinity, 0, 0) }],
        [new TourListeAntwort([], 0, 0, 50) { Tabellensumme = new TourenTabellensummeAntwort(0, 0, 1, 0) }]
    ];
}
