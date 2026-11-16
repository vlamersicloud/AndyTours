using System.Globalization;
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
        Assert.Collection(axis.Labels,
            label => Assert.Equal("Januar gekürzt", label),
            label => Assert.Equal("Februar leer", label),
            label => Assert.Equal("März geliefert", label));
        Assert.Collection(view.Diagramm.Series.Cast<RectangleBarSeries>(),
            series => Assert.Equal("Eigene Zone A · 90–99 bpm", series.Title),
            series => Assert.Equal("Eigene Zone B · 100–109 bpm", series.Title));
        var series = view.Diagramm.Series.Cast<RectangleBarSeries>().ToArray();
        Assert.NotEqual(series[0].FillColor, series[1].FillColor);
        Assert.Single(series[0].Items);
        Assert.Single(series[1].Items);
        Assert.Collection(view.Zeilen,
            row => { Assert.Equal("Januar gekürzt", row.Zeitraum); Assert.Equal("Eigene Zone A", row.Zone); Assert.Equal("2,0 min", row.Messzeit); Assert.Equal((.25).ToString("P1", CultureInfo.GetCultureInfo("de-DE")), row.Anteil); },
            row => { Assert.Equal("März geliefert", row.Zeitraum); Assert.Equal("Eigene Zone B", row.Zone); Assert.Equal("1,0 min", row.Messzeit); Assert.Equal((.75).ToString("P1", CultureInfo.GetCultureInfo("de-DE")), row.Anteil); });
    }
}
