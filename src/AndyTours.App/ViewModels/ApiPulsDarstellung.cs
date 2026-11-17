using System.Globalization;
using AndyTours.Contracts.V1;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace AndyTours.App.ViewModels;

public sealed record ApiPulszeile(string Zeitraum, string Zone, string Pulsbereich, string Messzeit, string Anteil);
public sealed record ApiPulsAnzeige(PlotModel Diagramm, IReadOnlyList<ApiPulszeile> Zeilen);

public static class ApiPulsDarstellung
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");
    private static readonly OxyColor[] Farben =
    [
        OxyColor.FromRgb(107, 114, 128), OxyColor.FromRgb(36, 109, 178),
        OxyColor.FromRgb(46, 125, 91), OxyColor.FromRgb(196, 148, 28),
        OxyColor.FromRgb(196, 80, 61), OxyColor.FromRgb(118, 86, 165),
        OxyColor.FromRgb(25, 140, 140), OxyColor.FromRgb(96, 125, 139),
        OxyColor.FromRgb(138, 143, 58), OxyColor.FromRgb(142, 90, 69)
    ];

    public static ApiPulsAnzeige Erstellen(PulsAuswertungAntwort antwort)
    {
        ArgumentNullException.ThrowIfNull(antwort);
        var model = new PlotModel { Culture = Deutsch, Background = OxyColors.White, PlotAreaBorderColor = OxyColor.FromRgb(194, 201, 209) };
        var cellsByKey = antwort.Zellen.ToDictionary(
            cell => (cell.Periode, cell.Bereich));

        var category = new CategoryAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = -.5,
            Maximum = antwort.Perioden.Count - .5,
            Angle = antwort.Perioden.Count > 16 ? -45 : 0,
            IsZoomEnabled = false,
            IsPanEnabled = false,
            TextColor = OxyColor.FromRgb(70, 86, 106),
            AxislineColor = OxyColor.FromRgb(143, 152, 163),
            MajorGridlineStyle = LineStyle.None
        };
        int stride = Math.Max(1, (int)Math.Ceiling(antwort.Perioden.Count / 12d));
        for (int i = 0; i < antwort.Perioden.Count; i++)
            category.Labels.Add(i % stride == 0 || i == antwort.Perioden.Count - 1
                ? antwort.Perioden[i].Beschriftung : string.Empty);
        model.Axes.Add(category);
        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Minuten",
            Minimum = 0,
            IsZoomEnabled = false,
            IsPanEnabled = false,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.FromRgb(232, 236, 240),
            MajorGridlineThickness = 1,
            TextColor = OxyColor.FromRgb(70, 86, 106),
            AxislineColor = OxyColor.FromRgb(143, 152, 163)
        });
        model.Legends.Add(new Legend
        {
            LegendPosition = LegendPosition.BottomCenter,
            LegendPlacement = LegendPlacement.Outside,
            LegendOrientation = LegendOrientation.Horizontal
        });

        var areaLookup = antwort.Bereiche.ToDictionary(area => area.Bereich);
        var areaIds = antwort.Zellen.Select(cell => cell.Bereich).Distinct().OrderBy(area => area).ToArray();
        var bottoms = new double[antwort.Perioden.Count];
        var rows = new List<(int PeriodIndex, int Area, ApiPulszeile Row)>();
        foreach (int areaId in areaIds)
        {
            if (!areaLookup.TryGetValue(areaId, out var area)) continue;
            var series = new RectangleBarSeries
            {
                Title = area.Zone + " · " + area.Pulsbereich,
                FillColor = Farben[Math.Abs((antwort.VerwendetesProfil is null ? areaId / 10 : areaId) % Farben.Length)],
                StrokeThickness = 0,
                LabelFormatString = null,
                TrackerFormatString = "{7}"
            };
            for (int i = 0; i < antwort.Perioden.Count; i++)
            {
                var period = antwort.Perioden[i];
                var key = period.Schluessel;
                if (!cellsByKey.TryGetValue((key, areaId), out var cell) || cell.Sekunden <= 0) continue;
                double minutes = cell.Sekunden / 60d;
                series.Items.Add(new RectangleBarItem(i - .35, bottoms[i], i + .35, bottoms[i] + minutes)
                {
                    Title = $"{period.Beschriftung} · {area.Zone} · {area.Pulsbereich}: {minutes.ToString("N1", Deutsch)} min"
                });
                bottoms[i] += minutes;
                rows.Add((i, areaId, new ApiPulszeile(period.Beschriftung, area.Zone, area.Pulsbereich,
                    minutes.ToString("N1", Deutsch) + " min", cell.Anteil.ToString("P1", Deutsch))));
            }
            model.Series.Add(series);
        }
        model.InvalidatePlot(true);
        return new ApiPulsAnzeige(model, rows.OrderBy(row => row.PeriodIndex).ThenBy(row => row.Area)
            .Select(row => row.Row).ToArray());
    }
}
