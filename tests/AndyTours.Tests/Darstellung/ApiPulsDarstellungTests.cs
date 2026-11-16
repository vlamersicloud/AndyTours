using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiPulsDarstellungTests
{
    [Fact]
    public void PreservesServerPeriodsLabelsSharesAndEmptyPeriods()
    {
        var reference = new PulsReferenzAntwort("v", DateTimeOffset.UnixEpoch, []);
        var janKey = new DateOnly(2026, 1, 1);
        var febKey = new DateOnly(2026, 2, 1);
        var marKey = new DateOnly(2026, 3, 1);
        var answer = new PulsAuswertungAntwort(new(2026, 1, 10), new(2026, 3, 31),
            Auswertungsraster.Monat, 3, 0, 240, 900, reference, null,
            [new(new(2026, 1, 10), new(2026, 1, 31), "Januar gekürzt") { Schluessel = janKey },
             new(new(2026, 2, 1), new(2026, 2, 28), "Februar leer") { Schluessel = febKey },
             new(new(2026, 3, 1), new(2026, 3, 31), "März geliefert") { Schluessel = marKey }],
            [new(90, "Eigene Zone A", "90–99 bpm"), new(100, "Eigene Zone B", "100–109 bpm")],
            [new(janKey, 90, 120, .25), new(marKey, 100, 60, .75)]);

        var view = ApiPulsDarstellung.Erstellen(answer);
        var axis = Assert.IsType<CategoryAxis>(Assert.Single(view.Diagramm.Axes, x => x.Position == AxisPosition.Bottom));
        Assert.Equal(new[] { "Januar gekürzt", "Februar leer", "März geliefert" }, axis.Labels);
        Assert.Equal(new[] { "Eigene Zone A · 90–99 bpm", "Eigene Zone B · 100–109 bpm" },
            view.Diagramm.Series.Cast<RectangleBarSeries>().Select(x => x.Title));
        var series = view.Diagramm.Series.Cast<RectangleBarSeries>().ToArray();
        Assert.NotEqual(series[0].FillColor, series[1].FillColor);
        Assert.All(series, s => Assert.Equal(1, s.Items.Count));
        Assert.Equal(2, view.Zeilen.Count);
        Assert.Equal(new[] { "Januar gekürzt", "März geliefert" }, view.Zeilen.Select(x => x.Zeitraum));
        Assert.Equal(new[] { "Eigene Zone A", "Eigene Zone B" }, view.Zeilen.Select(x => x.Zone));
        Assert.Equal("2,0 min", view.Zeilen[0].Messzeit);
        Assert.Equal("25,0 %", view.Zeilen[0].Anteil);
        Assert.Equal("1,0 min", view.Zeilen[1].Messzeit);
        Assert.Equal("75,0 %", view.Zeilen[1].Anteil);
    }
}
