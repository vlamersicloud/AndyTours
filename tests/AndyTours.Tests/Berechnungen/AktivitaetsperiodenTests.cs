using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;

namespace AndyTours.Tests.Berechnungen;

/// <summary>Kalendergrenzen, fehlende Perioden und Regression gegen Zukunftsjahre auf der Diagrammachse.</summary>
public sealed class AktivitaetsperiodenTests
{
    private static readonly double[] Monatswerte = [10d, 0d, 20d];
    [Fact]
    public void ZeitraumSchliesst2046AusUndFuelltLeereMonate()
    {
        var einheiten = new[]
        {
            Tour(new(2026, 1, 1), 10), Tour(new(2026, 3, 15), 20),
            Tour(new(2046, 3, 31), 500), Tour(new(2025, 12, 31), 400),
        };
        var result = BerechneAktivitaetsperioden.Ausfuehren(einheiten, new(2026, 1, 1), new(2026, 3, 15), Zeitraster.Monat);
        Assert.Equal(3, result.Count);
        Assert.Equal(Monatswerte, result.Select(p => p.DistanzKm));
        Assert.Equal(new DateOnly(2026, 3, 15), result[^1].Bis);
        Assert.Equal(2, result.Sum(p => p.Anzahl));
    }

    [Fact]
    public void IsoWocheBleibtUeberDenJahreswechselZusammen()
    {
        var result = BerechneAktivitaetsperioden.Ausfuehren(
            new[] { Tour(new(2020, 12, 31), 10), Tour(new(2021, 1, 1), 20) },
            new(2020, 12, 31), new(2021, 1, 4), Zeitraster.Woche);
        Assert.Equal(2, result.Count);
        Assert.Equal("KW 53/2020", result[0].Beschriftung);
        Assert.Equal(30, result[0].DistanzKm);
        Assert.Equal("KW 01/2021", result[1].Beschriftung);
    }

    [Fact]
    public void SchalttagUndRandtageSindInklusiv()
    {
        var result = BerechneAktivitaetsperioden.Ausfuehren(
            new[] { Tour(new(2024, 2, 29), 7) }, new(2024, 2, 28), new(2024, 3, 1), Zeitraster.Tag);
        Assert.Equal(3, result.Count);
        Assert.Equal(7, result[1].DistanzKm);
        Assert.Equal(150, result[1].Hoehenmeter);
        Assert.Equal(1.5, result[1].Fahrzeit.TotalHours);
    }

    [Fact]
    public void UngueltigerOderZuGrosserTageszeitraumWirdAbgewiesen()
    {
        Assert.Throws<ArgumentException>(() => BerechneAktivitaetsperioden.Ausfuehren([], new(2026, 2, 1), new(2026, 1, 1), Zeitraster.Tag));
        Assert.Throws<ArgumentException>(() => BerechneAktivitaetsperioden.Ausfuehren([], new(1900, 1, 1), new(2026, 1, 1), Zeitraster.Tag));
    }

    private static Trainingseinheit Tour(DateOnly datum, double km) => new()
    { Datum = datum, DistanzKm = km, Hoehenmeter = 150, Fahrzeit = TimeSpan.FromMinutes(90) };
}
