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
        OxyColors.Gray, OxyColors.SteelBlue, OxyColors.ForestGreen,
        OxyColors.Goldenrod, OxyColors.OrangeRed, OxyColors.Purple,
        OxyColors.Teal, OxyColors.SlateBlue, OxyColors.OliveDrab, OxyColors.Sienna
    ];

    public static ApiPulsAnzeige Erstellen(PulsAuswertungAntwort antwort)
    {
        ArgumentNullException.ThrowIfNull(antwort);
        var model = new PlotModel { Culture = Deutsch, PlotAreaBorderColor = OxyColors.LightGray };
        var periodKeys = antwort.Perioden.Select(period => period.Schluessel).ToHashSet();
        var grouped = antwort.Zellen
            .Where(cell => periodKeys.Contains(cell.Periode))
            .GroupBy(cell => (cell.Periode, cell.Bereich))
            .ToDictionary(group => group.Key, group => group.Sum(cell => cell.Sekunden));

        var category = new CategoryAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = -.5,
            Maximum = antwort.Perioden.Count - .5,
            Angle = antwort.Perioden.Count > 16 ? -45 : 0,
            IsZoomEnabled = false,
            IsPanEnabled = false
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
            MajorGridlineStyle = LineStyle.Dot
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
                FillColor = Farben[Math.Abs(areaId % Farben.Length)],
                StrokeThickness = 0,
                LabelFormatString = null,
                TrackerFormatString = "{7}"
            };
            for (int i = 0; i < antwort.Perioden.Count; i++)
            {
                var period = antwort.Perioden[i];
                var key = period.Schluessel;
                var cell = antwort.Zellen.FirstOrDefault(item => item.Periode == key && item.Bereich == areaId);
                if (cell is null || cell.Sekunden <= 0) continue;
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
