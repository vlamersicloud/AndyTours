using System.Collections.ObjectModel;
using System.Globalization;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Client;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace AndyTours.App.ViewModels;

/// <summary>Stellt serverseitige Tourenkennzahlen und paginierte Touren für die API-Ansicht dar.</summary>
public partial class ApiUebersichtViewModel : ObservableObject
{
    private readonly IAktivitaetsauswertung _auswertung;
    private readonly ITourenSpeicher _touren;
    private readonly ObservableCollection<AktivitaetsperiodenAntwort> _perioden = [];
    private long _generation;
    private int _start;

    [ObservableProperty] private DateTime? _vonDatum = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? _bisDatum = DateTime.Today;
    [ObservableProperty] private Auswertungsraster _raster = Auswertungsraster.Monat;
    [ObservableProperty] private ApiDiagrammkennzahl _kennzahl = ApiDiagrammkennzahl.Kilometer;
    [ObservableProperty] private PlotModel _diagramm = new() { Background = OxyColors.White };
    [ObservableProperty] private string _distanzKmText = "–";
    [ObservableProperty] private string _hoehenmeterText = "–";
    [ObservableProperty] private string _fahrzeitText = "–";
    [ObservableProperty] private string _anzahlText = "–";
    [ObservableProperty] private string _statusText = "Filter wählen und aktualisieren.";
    [ObservableProperty] private string _zukunftsHinweis = string.Empty;
    [ObservableProperty] private bool _laedt;
    [ObservableProperty] private int _gesamtTouren;
    [ObservableProperty] private string _seitenText = "Keine Daten geladen";

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Initialisiert die Übersicht ausschließlich mit typisierten API-Ports.</summary>
    public ApiUebersichtViewModel(IAktivitaetsauswertung auswertung, ITourenSpeicher touren)
    {
        _auswertung = auswertung;
        _touren = touren;
        Touren = [];
    }

    /// <summary>Servergeladene Tourenseite.</summary>
    public ObservableCollection<TourAntwort> Touren { get; }

    /// <summary>Verfügbare Zeitraster und Anzeigenamen.</summary>
    public IReadOnlyList<KeyValuePair<Auswertungsraster, string>> RasterOptionen { get; } =
    [
        new(Auswertungsraster.Jahr, "Jahr"), new(Auswertungsraster.Monat, "Monat"),
        new(Auswertungsraster.Woche, "Woche"), new(Auswertungsraster.Tag, "Tag")
    ];

    /// <summary>Verfügbare Diagrammkennzahlen.</summary>
    public IReadOnlyList<KeyValuePair<ApiDiagrammkennzahl, string>> KennzahlOptionen { get; } =
    [
        new(ApiDiagrammkennzahl.Kilometer, "Kilometer"), new(ApiDiagrammkennzahl.Hoehenmeter, "Höhenmeter"),
        new(ApiDiagrammkennzahl.Fahrzeit, "Fahrzeit"), new(ApiDiagrammkennzahl.Anzahl, "Anzahl")
    ];

    partial void OnKennzahlChanged(ApiDiagrammkennzahl value)
    {
        if (_perioden.Count > 0)
            ZeichneDiagramm();
    }

    partial void OnVonDatumChanged(DateTime? value) => FilterGeaendert();
    partial void OnBisDatumChanged(DateTime? value) => FilterGeaendert();
    partial void OnRasterChanged(Auswertungsraster value) => FilterGeaendert();

    private void FilterGeaendert()
    {
        Interlocked.Increment(ref _generation);
        _perioden.Clear();
        Diagramm = new PlotModel { Background = OxyColors.White };
        DistanzKmText = HoehenmeterText = FahrzeitText = AnzahlText = "–";
        ZukunftsHinweis = string.Empty;
        Touren.Clear();
        GesamtTouren = 0;
        _start = 0;
        SeitenText = "Keine Daten geladen";
        NaechsteSeiteCommand.NotifyCanExecuteChanged();
        VorherigeSeiteCommand.NotifyCanExecuteChanged();
        StatusText = "Filter geändert – bitte aktualisieren.";
    }

    /// <summary>Lädt Auswertung und erste Tourenseite parallel vom Backend.</summary>
    [RelayCommand(CanExecute = nameof(KannAktualisieren))]
    public async Task AktualisierenAsync(CancellationToken ct = default)
    {
        if (Laedt) return;
        Laedt = true;
        var generation = Interlocked.Increment(ref _generation);
        if (VonDatum is null || BisDatum is null || VonDatum > BisDatum)
        {
            StatusText = "Bitte einen gültigen Zeitraum auswählen.";
            return;
        }
        StatusText = "Daten werden geladen …";
        try
        {
            _start = 0;
            var von = DateOnly.FromDateTime(VonDatum.Value);
            var bis = DateOnly.FromDateTime(BisDatum.Value);
            var request = new AktivitaetsauswertungAnfrage(von, bis, Raster, null, null);
            var auswertungTask = _auswertung.Laden(request, ct);
            var tourenTask = _touren.Liste(_start, 50, von, bis, ct);
            await Task.WhenAll(auswertungTask, tourenTask);
            if (generation != Interlocked.Read(ref _generation)) return;

            var result = await auswertungTask;
            var page = await tourenTask;
            _perioden.Clear();
            foreach (var periode in result.Perioden) _perioden.Add(periode);
            DistanzKmText = $"{result.DistanzKm.ToString("N1", Deutsch)} km";
            HoehenmeterText = $"{result.Hoehenmeter.ToString("N0", Deutsch)} hm";
            FahrzeitText = DauerText(result.FahrzeitSekunden);
            AnzahlText = result.Anzahl.ToString("N0", Deutsch);
            ZukunftsHinweis = result.ZukuenftigeAktivitaeten > 0
                ? $"{result.ZukuenftigeAktivitaeten} zukünftige Aktivität(en) im Datenbestand."
                : string.Empty;
            SetzeSeite(page);
            ZeichneDiagramm();
            StatusText = page.Gesamt == 0 ? "Keine Touren im gewählten Zeitraum." : string.Empty;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (generation == Interlocked.Read(ref _generation)) StatusText = "Ladevorgang abgebrochen.";
        }
        catch (Exception)
        {
            if (generation == Interlocked.Read(ref _generation))
                StatusText = "Backenddaten konnten nicht geladen werden.";
        }
        finally
        {
            Laedt = false;
        }
    }

    /// <summary>Lädt die nächste Serverseite, falls vorhanden.</summary>
    [RelayCommand(CanExecute = nameof(KannNaechsteSeite))]
    public async Task NaechsteSeiteAsync(CancellationToken ct = default)
    {
        if (_start + 50 >= GesamtTouren) return;
        await LadeSeiteAsync(_start + 50, ct);
    }

    /// <summary>Lädt die vorherige Serverseite, falls vorhanden.</summary>
    [RelayCommand(CanExecute = nameof(KannVorherigeSeite))]
    public async Task VorherigeSeiteAsync(CancellationToken ct = default)
    {
        if (_start == 0) return;
        await LadeSeiteAsync(Math.Max(0, _start - 50), ct);
    }

    private bool KannAktualisieren() => !Laedt;
    partial void OnLaedtChanged(bool value)
    {
        AktualisierenCommand.NotifyCanExecuteChanged();
        NaechsteSeiteCommand.NotifyCanExecuteChanged();
        VorherigeSeiteCommand.NotifyCanExecuteChanged();
    }

    private bool KannNaechsteSeite() => !Laedt && _start + 50 < GesamtTouren;
    private bool KannVorherigeSeite() => !Laedt && _start > 0;

    private async Task LadeSeiteAsync(int start, CancellationToken ct)
    {
        if (Laedt || VonDatum is null || BisDatum is null || VonDatum > BisDatum) return;
        Laedt = true;
        var generation = Interlocked.Read(ref _generation);
        try
        {
            var page = await _touren.Liste(start, 50, DateOnly.FromDateTime(VonDatum.Value),
                DateOnly.FromDateTime(BisDatum.Value), ct);
            if (generation != Interlocked.Read(ref _generation)) return;
            _start = start;
            SetzeSeite(page);
            StatusText = string.Empty;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (generation == Interlocked.Read(ref _generation)) StatusText = "Ladevorgang abgebrochen.";
        }
        catch (Exception) { if (generation == Interlocked.Read(ref _generation)) StatusText = "Tourenseite konnte nicht geladen werden."; }
        finally { Laedt = false; }
    }

    private void SetzeSeite(TourListeAntwort page)
    {
        Touren.Clear();
        foreach (var tour in page.Eintraege) Touren.Add(tour);
        GesamtTouren = page.Gesamt;
        SeitenText = page.Gesamt == 0 ? "Keine Touren" : $"Seite {_start / 50 + 1} · {page.Gesamt} Touren";
        NaechsteSeiteCommand.NotifyCanExecuteChanged();
        VorherigeSeiteCommand.NotifyCanExecuteChanged();
    }

    private void ZeichneDiagramm()
    {
        var model = new PlotModel { Background = OxyColors.White };
        var zeitachse = new CategoryAxis { Position = AxisPosition.Bottom, Minimum = -0.5, Maximum = _perioden.Count - 0.5 };
        var einheit = Kennzahl switch
        {
            ApiDiagrammkennzahl.Kilometer => "km",
            ApiDiagrammkennzahl.Hoehenmeter => "hm",
            ApiDiagrammkennzahl.Fahrzeit => "Stunden",
            _ => "Touren"
        };
        model.Axes.Add(zeitachse);
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Minimum = 0, Title = einheit });
        var bars = new RectangleBarSeries { FillColor = OxyColor.FromRgb(0, 102, 204), StrokeThickness = 0 };
        for (var i = 0; i < _perioden.Count; i++)
        {
            var periode = _perioden[i];
            zeitachse.Labels.Add(periode.Beschriftung);
            var wert = Kennzahl switch
            {
                ApiDiagrammkennzahl.Kilometer => periode.DistanzKm,
                ApiDiagrammkennzahl.Hoehenmeter => periode.Hoehenmeter,
                ApiDiagrammkennzahl.Fahrzeit => periode.FahrzeitSekunden / 3600d,
                ApiDiagrammkennzahl.Anzahl => periode.Anzahl,
                _ => 0d
            };
            bars.Items.Add(new RectangleBarItem(i - .35, 0, i + .35, wert));
        }
        model.Series.Add(bars);
        Diagramm = model;
    }

    private static string DauerText(long sekunden)
    {
        var stunden = sekunden / 3600;
        var minuten = sekunden % 3600 / 60;
        return $"{stunden.ToString("N0", Deutsch)}:{minuten:00} h";
    }
}

/// <summary>Vom Benutzer gewählte Kennzahl des Balkendiagramms.</summary>
public enum ApiDiagrammkennzahl { Kilometer, Hoehenmeter, Fahrzeit, Anzahl }
