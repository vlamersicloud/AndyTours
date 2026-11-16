using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;

namespace AndyTours.App.ViewModels;

/// <summary>Zeitraumbezogene Aktivitätsübersicht mit expliziten Kalender- und Kennzahlfiltern.</summary>
public partial class JahresfortschrittViewModel : ObservableObject
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");
    private Datenbestand? _bestand;
    private long? _aktivitaetId;
    private string? _kategorie;
    private string _datenordner = string.Empty;
    private DateOnly _heute = DateOnly.FromDateTime(DateTime.Today);
    private bool _standardZeitraum = true, _ladeAuswahl;
    private int _generation;
    private readonly CategoryAxis _zeitachse = new() { Position = AxisPosition.Bottom, IsPanEnabled = false, IsZoomEnabled = false };
    private readonly LinearAxis _wertachse = new() { Key = "strecke", Position = AxisPosition.Left, Minimum = 0, MajorGridlineStyle = LineStyle.Dot, IsPanEnabled = false, IsZoomEnabled = false };

    private readonly LinearAxis _hoehenachse = new() { Key = "hoehe", Position = AxisPosition.Right, Title = "Höhenmeter (hm)", Minimum = 0, IsPanEnabled = false, IsZoomEnabled = false };
    private readonly LinearAxis _pulsachse = new() { Key = "puls", Position = AxisPosition.Right, PositionTier = 1, Title = "Pulsbereiche (Minuten)", Minimum = 0, IsPanEnabled = false, IsZoomEnabled = false };
    private readonly List<RectangleBarSeries> _pulsserien = [];

    /// <summary>Darstellungsart gilt gemeinsam für alle ausgewählten Kennzahlen.</summary>
    public IReadOnlyList<string> Darstellungen { get; } = ["Balken", "Linie"];
    [ObservableProperty] private int _darstellung;
    partial void OnDarstellungChanged(int value) => Zeichne();

    [ObservableProperty] private bool _zeigeStrecke = true;
    [ObservableProperty] private bool _zeigeHoehenmeter;
    [ObservableProperty] private bool _zeigePuls;

    partial void OnZeigeStreckeChanged(bool value) => Zeichne();
    partial void OnZeigeHoehenmeterChanged(bool value) => Zeichne();
    partial void OnZeigePulsChanged(bool value) => Zeichne();

    /// <summary>Blendet Pulsreihen aus, ohne die geladene Analyse zu verwerfen.</summary>
    [RelayCommand]
    private void PulsAusblenden() => ZeigePuls = false;

    /// <summary>Aktivität und Kategorie wirken wie bisher; der Zeitraum wird hier ausdrücklich unabhängig vom Tabellen-Jahresfilter eingestellt.</summary>
    public event EventHandler? ZeitraumGeaendert;

    /// <summary>Explizite Kennzahlen mit lokalisierter Anzeige.</summary>
    public IReadOnlyList<KeyValuePair<Fortschrittswert, string>> Werte { get; } =
    [
        new(Fortschrittswert.Kilometer, "Gesamtkilometer"),
        new(Fortschrittswert.Hoehenmeter, "Höhenmeter"),
        new(Fortschrittswert.Fahrzeit, "Fahrzeit"),
        new(Fortschrittswert.Anzahl, "Anzahl Aktivitäten"),
    ];

    /// <summary>ISO-Kalenderwochen beginnen montags; keine gleitenden Siebentagefenster.</summary>
    public IReadOnlyList<KeyValuePair<Zeitraster, string>> Raster { get; } =
    [
        new(Zeitraster.Jahr, "Jahr"), new(Zeitraster.Monat, "Monat"),
        new(Zeitraster.Woche, "Kalenderwoche (ISO)"), new(Zeitraster.Tag, "Tag"),
    ];

    [ObservableProperty] private DateTime? _vonDatum = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _bisDatum = DateTime.Today;
    [ObservableProperty] private Zeitraster _gruppierung = Zeitraster.Monat;
    [ObservableProperty] private Fortschrittswert _wert = Fortschrittswert.Kilometer;
    [ObservableProperty] private string _ueberschrift = "Aktivitäten";
    [ObservableProperty] private string _stand = string.Empty;
    [ObservableProperty] private string _ausblick = string.Empty;
    [ObservableProperty] private string _zeitraumHinweis = string.Empty;
    [ObservableProperty] private bool _hatEinheiten;

    /// <summary>Balken statt einer scheinbar kontinuierlichen Fläche über voneinander getrennten Jahren.</summary>
    public PlotModel Diagramm { get; } = new() { Culture = Deutsch, PlotAreaBorderColor = OxyColors.LightGray };

    /// <summary>Initialisiert die Diagrammachsen.</summary>
    public JahresfortschrittViewModel()
    {
        Diagramm.Axes.Add(_zeitachse);
        Diagramm.Axes.Add(_wertachse);
        Diagramm.Axes.Add(_hoehenachse);
        Diagramm.Axes.Add(_pulsachse);
        Diagramm.Legends.Add(new OxyPlot.Legends.Legend {
            LegendPosition = OxyPlot.Legends.LegendPosition.BottomCenter,
            LegendPlacement = OxyPlot.Legends.LegendPlacement.Outside,
            LegendOrientation = OxyPlot.Legends.LegendOrientation.Horizontal });
    }

    /// <summary>Gültiger einschließlich begrenzter Zeitraum für Diagramm, Rekorde und Pulsauswertung.</summary>
    public DateOnly? Von => VonDatum is DateTime v ? DateOnly.FromDateTime(v) : null;
    public DateOnly? Bis => BisDatum is DateTime b ? DateOnly.FromDateTime(b) : null;

    /// <summary>Übernimmt geänderten Datenbestand; ein Datenordnerwechsel verwirft Pulsdaten und Zonen des alten Ordners.</summary>
    public void Aktualisiere(Datenbestand bestand, long? aktivitaetId, string? kategorie, DateOnly heute, string datenordner)
    {
        bool ordnerwechsel = _datenordner != datenordner;
        _bestand = bestand; _aktivitaetId = aktivitaetId; _kategorie = kategorie; _heute = heute; _datenordner = datenordner;
        if (_standardZeitraum)
        {
            _ladeAuswahl = true;
            VonDatum = new DateTime(heute.Year, 1, 1);
            BisDatum = heute.ToDateTime(TimeOnly.MinValue);
            _ladeAuswahl = false;
        }

        if (ordnerwechsel) { LadeGespeicherteZonen(); }
        AuswahlGeaendert();
    }

    partial void OnVonDatumChanged(DateTime? value) { DatumGeaendert(); }
    partial void OnBisDatumChanged(DateTime? value) { DatumGeaendert(); }
    partial void OnGruppierungChanged(Zeitraster value) { AuswahlGeaendert(); }
    partial void OnWertChanged(Fortschrittswert value) { Zeichne(); }

    /// <summary>Datumsentwürfe werden nicht stillschweigend korrigiert; ungültige Auswahl zeigt einen Hinweis.</summary>
    private void DatumGeaendert()
    {
        if (_ladeAuswahl) { return; }
        _standardZeitraum = false;
        AuswahlGeaendert();
    }

    /// <summary>Setzt ausdrücklich auf 1. Januar des aktuellen Jahres bis heute zurück.</summary>
    [RelayCommand]
    private void AktuellesJahr()
    {
        _heute = DateOnly.FromDateTime(DateTime.Today);
        _ladeAuswahl = true;
        VonDatum = new DateTime(_heute.Year, 1, 1);
        BisDatum = _heute.ToDateTime(TimeOnly.MinValue);
        _ladeAuswahl = false;
        _standardZeitraum = true;
        AuswahlGeaendert();
    }

    /// <summary>Bereitet die ausgewählten Aktivitäten vor; keine Ausweitung auf Zukunftsjahre durch automatische Achsenskalierung.</summary>
    private List<Trainingseinheit> Auswahl() => _bestand is null ? [] :
        FiltereUebersicht.Ausfuehren(_bestand, _aktivitaetId, _kategorie);

    /// <summary>Verwirft Pulsanalysen nach Änderung ihrer Eingabedaten; alte Resultate werden nie für neue Filter ausgegeben.</summary>
    private void AuswahlGeaendert()
    {
        _generation++;
        LeerePulsanalyse();
        Zeichne();
        ZeitraumGeaendert?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Zeichnet ausschließlich Summen innerhalb der sichtbaren Datumsgrenzen, einschließlich leerer Perioden.</summary>
    private void Zeichne()
    {
        OnPropertyChanged(nameof(Von)); OnPropertyChanged(nameof(Bis));
        Diagramm.Series.Clear(); _zeitachse.Labels.Clear();
        HatEinheiten = false; Stand = string.Empty; Ausblick = string.Empty;
        if (Von is not DateOnly von || Bis is not DateOnly bis || von > bis)
        {
            ZeitraumHinweis = "Bitte einen gültigen Zeitraum mit Von ≤ Bis eingeben.";
            Diagramm.InvalidatePlot(true);
            return;
        }

        List<Trainingseinheit> auswahl = Auswahl();
        IReadOnlyList<Aktivitaetsperiode> perioden;
        try { perioden = BerechneAktivitaetsperioden.Ausfuehren(auswahl, von, bis, Gruppierung); }
        catch (ArgumentException fehler) { ZeitraumHinweis = fehler.Message; Diagramm.InvalidatePlot(true); return; }
        HatEinheiten = perioden.Sum(p => p.Anzahl) > 0;
        _wertachse.IsAxisVisible = ZeigeStrecke;
        _hoehenachse.IsAxisVisible = ZeigeHoehenmeter;
        _pulsachse.IsAxisVisible = ZeigePuls;
        string einheit = Wert switch { Fortschrittswert.Kilometer => "km", Fortschrittswert.Hoehenmeter => "hm", Fortschrittswert.Anzahl => "Aktivitäten", _ => "Stunden" };
        Ueberschrift = "Strecke / Puls / Höhenmeter je " + Raster.First(r => r.Key == Gruppierung).Value;
        _wertachse.Title = "Strecke (km)";
        _zeitachse.Minimum = -.5; _zeitachse.Maximum = perioden.Count - .5;
        _zeitachse.Angle = perioden.Count > 16 ? -45 : 0;
        _zeitachse.GapWidth = .3;
        double gesamt = 0;
        for (int i = 0; i < perioden.Count; i++)
        {
            Aktivitaetsperiode p = perioden[i];
            double y = Wert switch { Fortschrittswert.Kilometer => p.DistanzKm, Fortschrittswert.Hoehenmeter => p.Hoehenmeter, Fortschrittswert.Anzahl => p.Anzahl, _ => p.Fahrzeit.TotalHours };
            gesamt += y; _zeitachse.Labels.Add(p.Beschriftung);
        }

        if (ZeigeStrecke) ZeichneKennzahl(perioden, false);
        if (ZeigeHoehenmeter) ZeichneKennzahl(perioden, true);
        if (ZeigePuls)
        {
            foreach (var pulsserie in _pulsserien) Diagramm.Series.Add(pulsserie);
        }
        Stand = $"{von:dd.MM.yyyy} – {bis:dd.MM.yyyy}: {gesamt.ToString("N2", Deutsch)} {einheit} · {perioden.Sum(p => p.Anzahl)} Aktivitäten";
        int zukunft = auswahl.Count(e => e.Datum > _heute);
        ZeitraumHinweis = zukunft > 0 ? $"{zukunft} Aktivität(en) tragen ein Datum nach heute. Daten wurden nicht geändert; nur der gewählte Zeitraum wird ausgewertet." : string.Empty;
        Ausblick = "Zeitraum gilt für Diagramm, Rekorde und Pulsanalyse. Aktivität/Kategorie wirken mit; der Tabellen-Jahresfilter ist unabhängig.";
        if (von == new DateOnly(_heute.Year, 1, 1) && bis == _heute && _bestand is not null)
        {
            var begrenzt = new Datenbestand { Trainingseinheiten = _bestand.Trainingseinheiten.Where(e => e.Datum <= _heute).ToList(),
                Aktivitaeten = _bestand.Aktivitaeten, Kategorien = _bestand.Kategorien, Saisonziele = _bestand.Saisonziele };
            (string zielstand, string zielausblick) = BeschreibeJahresfortschritt.Kennzahlen(
                ErmittleJahresfortschritt.Ausfuehren(begrenzt, _aktivitaetId, _kategorie, Wert, _heute));
            Ausblick += "\nSaison: " + zielstand + " · " + zielausblick;
        }

        if (Darstellung == 1)
        {
            // Pulsbalken sind gestapelt: als Linien die einzelnen Bereichszeiten,
            // nicht die kumulierten Oberkanten darstellen.
            var balken = Diagramm.Series.OfType<RectangleBarSeries>().ToArray();
            Diagramm.Series.Clear();
            foreach (var quelle in balken)
            {
                var linie = new LineSeries {
                    Title = quelle.Title, YAxisKey = quelle.YAxisKey,
                    Color = quelle.FillColor,
                    StrokeThickness = quelle.YAxisKey == "strecke" && ZeigePuls ? 4 : 2,
                    MarkerType = MarkerType.Circle, MarkerSize = 3,
                    TrackerFormatString = "{0}: {4:0.##}" };
                var werte = quelle.Items.ToDictionary(
                    item => (int)Math.Round((item.X0 + item.X1) / 2),
                    item => item.Y1 - item.Y0);
                for (int i = 0; i < perioden.Count; i++)
                {
                    linie.Points.Add(new DataPoint(i, werte.GetValueOrDefault(i)));
                }
                Diagramm.Series.Add(linie);
            }
        }

        Diagramm.InvalidatePlot(true);
    }
    private void ZeichneKennzahl(IReadOnlyList<Aktivitaetsperiode> perioden, bool hoehe)
    {
        var serie = new RectangleBarSeries {
            Title = hoehe ? "Höhenmeter (hm)" : "Strecke (km)",
            YAxisKey = hoehe ? "hoehe" : "strecke",
            FillColor = hoehe ? OxyColors.ForestGreen : OxyColor.Parse("#2E6DB4"),
            StrokeThickness = 0, LabelFormatString = null, TrackerFormatString = "{7}" };
        for (int i = 0; i < perioden.Count; i++)
        {
            double wert = hoehe ? perioden[i].Hoehenmeter : perioden[i].DistanzKm;
            double links = hoehe ? -.12 : -.42;
            double rechts = hoehe ? .12 : -.15;
            serie.Items.Add(new RectangleBarItem(i + links, 0, i + rechts, wert) {
                Title = perioden[i].Beschriftung + ": " + wert.ToString("N2", Deutsch) + (hoehe ? " hm" : " km") });
        }
        Diagramm.Series.Add(serie);
    }

}
