using System.Collections.ObjectModel;
using System.Globalization;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Stammdaten;
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
    private readonly IStammdatenLeser _stammdatenLeser;
    private readonly ObservableCollection<AktivitaetsperiodenAntwort> _perioden = [];
    private long _generation;
    private int _start;
    private bool _filterAenderungSuspendiert;
    private bool _lookupsGeladen;

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
    [ObservableProperty] private TourAntwort? _ausgewaehlteTour;
    [ObservableProperty] private ApiFilterOption? _aktivitaetAuswahl;
    [ObservableProperty] private ApiFilterOption? _kategorieAuswahl;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Initialisiert die Übersicht ausschließlich mit typisierten API-Ports.</summary>
    public ApiUebersichtViewModel(IAktivitaetsauswertung auswertung, ITourenSpeicher touren, IStammdatenLeser stammdatenLeser)
    {
        _auswertung = auswertung;
        _touren = touren;
        _stammdatenLeser = stammdatenLeser;
        Touren = [];
        AktivitaetsOptionen = [new ApiFilterOption(null, "Alle Aktivitäten")];
        KategorieOptionen = [new ApiFilterOption(null, "Alle Kategorien")];
    }

    /// <summary>Servergeladene Tourenseite.</summary>
    public ObservableCollection<TourAntwort> Touren { get; }

    /// <summary>Aktivitätsfilter einschließlich der Option ohne Einschränkung.</summary>
    public ObservableCollection<ApiFilterOption> AktivitaetsOptionen { get; }

    /// <summary>Kategorienfilter einschließlich der Option ohne Einschränkung.</summary>
    public ObservableCollection<ApiFilterOption> KategorieOptionen { get; }

    /// <summary>Gibt an, ob die Filtereingaben freigegeben sind.</summary>
    public bool FilterControlsEnabled => !Laedt;

    /// <summary>Verfügbare Zeitraster und Anzeigenamen.</summary>
    public IReadOnlyList<KeyValuePair<Auswertungsraster, string>> RasterOptionen { get; } =
    [
        new(Auswertungsraster.Jahr, "Jahr"), new(Auswertungsraster.Monat, "Monat"),
        new(Auswertungsraster.Woche, "Kalenderwoche (ISO)"), new(Auswertungsraster.Tag, "Tag")
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
    partial void OnAktivitaetAuswahlChanged(ApiFilterOption? value)
    {
        if (!_filterAenderungSuspendiert) FilterGeaendert();
    }
    partial void OnKategorieAuswahlChanged(ApiFilterOption? value)
    {
        if (!_filterAenderungSuspendiert) FilterGeaendert();
    }

    private void FilterGeaendert()
    {
        Interlocked.Increment(ref _generation);
        LeereAnzeige();
        StatusText = "Filter geändert – bitte aktualisieren.";
    }

    private void LeereAnzeige()
    {
        _perioden.Clear();
        AusgewaehlteTour = null;
        Diagramm = new PlotModel { Background = OxyColors.White };
        DistanzKmText = HoehenmeterText = FahrzeitText = AnzahlText = "–";
        ZukunftsHinweis = string.Empty;
        Touren.Clear();
        GesamtTouren = 0;
        _start = 0;
        SeitenText = "Keine Daten geladen";
        NaechsteSeiteCommand.NotifyCanExecuteChanged();
        VorherigeSeiteCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Lädt Auswertung und erste Tourenseite parallel vom Backend.</summary>
    [RelayCommand(CanExecute = nameof(KannAktualisieren))]
    public async Task AktualisierenAsync(CancellationToken ct = default)
    {
        if (Laedt) return;
        var generation = Interlocked.Increment(ref _generation);
        Laedt = true;
        StatusText = "Filter und Stammdaten werden geladen …";
        try
        {
            var (lookupsAktuell, lookupHinweis) = await LadeFilterOptionenAsync(generation, ct);
            if (!lookupsAktuell || generation != Interlocked.Read(ref _generation)) return;
            if (VonDatum is null || BisDatum is null || VonDatum > BisDatum)
            {
                StatusText = "Bitte einen gültigen Zeitraum auswählen.";
                return;
            }

            _start = 0;
            var von = DateOnly.FromDateTime(VonDatum.Value);
            var bis = DateOnly.FromDateTime(BisDatum.Value);
            var aktivitaetId = AktivitaetAuswahl?.Id;
            var kategorieId = KategorieAuswahl?.Id;
            var request = new AktivitaetsauswertungAnfrage(von, bis, Raster, aktivitaetId, kategorieId);
            var filter = new TourenListenfilter(von, bis, aktivitaetId, kategorieId);
            var auswertungTask = _auswertung.Laden(request, ct);
            var tourenTask = _touren.ListeGefiltert(_start, 50, filter, ct);
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
            StatusText = lookupHinweis ?? (page.Gesamt == 0
                ? "Keine Touren im gewählten Zeitraum und Filter."
                : string.Empty);
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

    private async Task<(bool Aktuell, string? Hinweis)> LadeFilterOptionenAsync(long generation, CancellationToken ct)
    {
        var vorherAktivitaetId = AktivitaetAuswahl?.Id;
        var vorherKategorieId = KategorieAuswahl?.Id;
        var stammdaten = await _stammdatenLeser.Laden(ct);
        if (generation != Interlocked.Read(ref _generation)) return (false, null);

        var aktivitaeten = stammdaten.Aktivitaeten
            .Select(a => new ApiFilterOption(a.Id, a.Name)).ToArray();
        var kategorien = stammdaten.Kategorien
            .OrderBy(k => k.Name, StringComparer.Create(Deutsch, ignoreCase: false))
            .Select(k => new ApiFilterOption(k.Id, k.Name)).ToArray();

        ApiFilterOption? neueAktivitaetsauswahl = vorherAktivitaetId is { } aktivitaetId
            ? aktivitaeten.FirstOrDefault(a => a.Id == aktivitaetId)
            : (!_lookupsGeladen ? aktivitaeten.FirstOrDefault(a =>
                stammdaten.Aktivitaeten.First(source => source.Id == a.Id).IstHaupt) : null);
        ApiFilterOption? neueKategorieauswahl = vorherKategorieId is { } kategorieId
            ? kategorien.FirstOrDefault(k => k.Id == kategorieId)
            : null;

        string? hinweis = null;
        if (vorherAktivitaetId.HasValue && neueAktivitaetsauswahl is null)
            hinweis = "Die zuvor gewählte Aktivität ist nicht mehr vorhanden; es wird jetzt nach allen Aktivitäten gefiltert.";
        else if (vorherKategorieId.HasValue && neueKategorieauswahl is null)
            hinweis = "Die zuvor gewählte Kategorie ist nicht mehr vorhanden; es wird jetzt nach allen Kategorien gefiltert.";
        else if (!_lookupsGeladen && !vorherAktivitaetId.HasValue &&
                 stammdaten.Aktivitaeten.Count > 0 && neueAktivitaetsauswahl is null)
            hinweis = "Keine Hauptaktivität vorhanden; es wird nach allen Aktivitäten gefiltert.";

        var lookupFilterGeaendert =
            vorherAktivitaetId != neueAktivitaetsauswahl?.Id ||
            vorherKategorieId != neueKategorieauswahl?.Id;
        if (lookupFilterGeaendert)
            LeereAnzeige();

        _filterAenderungSuspendiert = true;
        try
        {
            AktivitaetsOptionen.Clear();
            AktivitaetsOptionen.Add(new ApiFilterOption(null, "Alle Aktivitäten"));
            foreach (var option in aktivitaeten) AktivitaetsOptionen.Add(option);

            KategorieOptionen.Clear();
            KategorieOptionen.Add(new ApiFilterOption(null, "Alle Kategorien"));
            foreach (var option in kategorien) KategorieOptionen.Add(option);

            AktivitaetAuswahl = neueAktivitaetsauswahl ?? AktivitaetsOptionen[0];
            KategorieAuswahl = neueKategorieauswahl ?? KategorieOptionen[0];
        }
        finally
        {
            _filterAenderungSuspendiert = false;
        }

        _lookupsGeladen = true;
        OnPropertyChanged(nameof(AktivitaetsOptionen));
        OnPropertyChanged(nameof(KategorieOptionen));
        return (true, hinweis);
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
        OnPropertyChanged(nameof(FilterControlsEnabled));
        AktualisierenCommand.NotifyCanExecuteChanged();
        NaechsteSeiteCommand.NotifyCanExecuteChanged();
        VorherigeSeiteCommand.NotifyCanExecuteChanged();
        BenachrichtigeTourCommands();
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
            var filter = new TourenListenfilter(DateOnly.FromDateTime(VonDatum.Value),
                DateOnly.FromDateTime(BisDatum.Value), AktivitaetAuswahl?.Id, KategorieAuswahl?.Id);
            var page = await _touren.ListeGefiltert(start, 50, filter, ct);
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
        AusgewaehlteTour = null;
    }

    private void ZeichneDiagramm()
    {
        var model = new PlotModel { Background = OxyColors.White };
        var zeitachse = new CategoryAxis { Position = AxisPosition.Bottom, Minimum = -0.5, Maximum = _perioden.Count - 0.5, IsPanEnabled = false, IsZoomEnabled = false, Angle = _perioden.Count > 16 ? -45 : 0, MajorStep = _perioden.Count > 16 ? 2 : 1 };
        var einheit = Kennzahl switch
        {
            ApiDiagrammkennzahl.Kilometer => "km",
            ApiDiagrammkennzahl.Hoehenmeter => "hm",
            ApiDiagrammkennzahl.Fahrzeit => "Stunden",
            _ => "Touren"
        };
        model.Axes.Add(zeitachse);
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Minimum = 0, Title = einheit, IsPanEnabled = false, IsZoomEnabled = false });
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

/// <summary>UI-Auswahlwert mit optionaler Ressourcen-ID für „alle“.</summary>
public sealed record ApiFilterOption(long? Id, string Name);
