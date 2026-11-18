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
    public ObservableCollection<BestleistungszeileAntwort> Jahresrekorde { get; } = [];
    public ObservableCollection<BestleistungszeileAntwort> Einzelrekorde { get; } = [];
    public ObservableCollection<JahreszielAntwort> Jahresziele { get; } = [];
    private ApiSaisonTexte _saisonTexte = ApiSaisonTexte.Leer;
    private long _generation;
    private long _aktualisierungsstand;
    private int _start;
    private bool _filterAenderungSuspendiert;
    private bool _lookupsGeladen;
    private string? _angewandteAuswahlQuery;

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
    [ObservableProperty] private string _saisonText = string.Empty;
    [ObservableProperty] private string _zukunftsHinweis = string.Empty;
    [ObservableProperty] private bool _laedt;
    [ObservableProperty] private int _gesamtTouren;
    [ObservableProperty] private string _seitenText = "Keine Daten geladen";
    [ObservableProperty] private string _tabellensummeText = "Keine Tabellensumme geladen.";
    [ObservableProperty] private TourAntwort? _ausgewaehlteTour;
    [ObservableProperty] private ApiFilterOption? _aktivitaetAuswahl;
    [ObservableProperty] private ApiFilterOption? _kategorieAuswahl;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    public ApiUebersichtViewModel(IAktivitaetsauswertung auswertung, ITourenSpeicher touren, IStammdatenLeser stammdatenLeser)
    {
        _auswertung = auswertung;
        _touren = touren;
        _stammdatenLeser = stammdatenLeser;
        Tabellenfilter.Geaendert += TabellenfilterGeaendert;
        Touren = [];
        AktivitaetsOptionen = [new ApiFilterOption(null, "Alle Aktivitäten")];
        KategorieOptionen = [new ApiFilterOption(null, "Alle Kategorien")];
    }

    public ObservableCollection<TourAntwort> Touren { get; }
    public ApiTourTabellenfilterViewModel Tabellenfilter { get; } = new();
    public long Aktualisierungsstand => _aktualisierungsstand;
    public ObservableCollection<ApiFilterOption> AktivitaetsOptionen { get; }
    public ObservableCollection<ApiFilterOption> KategorieOptionen { get; }
    public bool FilterControlsEnabled => !Laedt && !InlineGesperrt;

    public IReadOnlyList<KeyValuePair<Auswertungsraster, string>> RasterOptionen { get; } =
    [
        new(Auswertungsraster.Jahr, "Jahr"), new(Auswertungsraster.Monat, "Monat"),
        new(Auswertungsraster.Woche, "Kalenderwoche (ISO)"), new(Auswertungsraster.Tag, "Tag")
    ];

    public IReadOnlyList<KeyValuePair<ApiDiagrammkennzahl, string>> KennzahlOptionen { get; } =
    [
        new(ApiDiagrammkennzahl.Kilometer, "Kilometer"), new(ApiDiagrammkennzahl.Hoehenmeter, "Höhenmeter"),
        new(ApiDiagrammkennzahl.Fahrzeit, "Fahrzeit"), new(ApiDiagrammkennzahl.Anzahl, "Anzahl")
    ];

    partial void OnKennzahlChanged(ApiDiagrammkennzahl value)
    {
        AktualisiereSaisonText();
        if (_perioden.Count > 0) ZeichneDiagramm();
    }

    private void AktualisiereSaisonText() => SaisonText = Kennzahl switch
    {
        ApiDiagrammkennzahl.Kilometer => _saisonTexte.Kilometer,
        ApiDiagrammkennzahl.Hoehenmeter => _saisonTexte.Hoehenmeter,
        ApiDiagrammkennzahl.Anzahl => _saisonTexte.Anzahl,
        ApiDiagrammkennzahl.Fahrzeit => _saisonTexte.Fahrzeit,
        _ => string.Empty
    };

    partial void OnVonDatumChanged(DateTime? value) => FilterGeaendert();
    partial void OnBisDatumChanged(DateTime? value) => FilterGeaendert();
    partial void OnRasterChanged(Auswertungsraster value) => FilterGeaendert();
    partial void OnAktivitaetAuswahlChanged(ApiFilterOption? value) { if (!_filterAenderungSuspendiert) FilterGeaendert(); }
    partial void OnKategorieAuswahlChanged(ApiFilterOption? value) { if (!_filterAenderungSuspendiert) FilterGeaendert(); }

    private void TabellenfilterGeaendert()
    {
        if (MerkeInlineFilterAenderung(nurTabelle: true)) return;
        Interlocked.Increment(ref _generation);
        Touren.Clear();
        AusgewaehlteTour = null;
        GesamtTouren = 0;
        _start = 0;
        SeitenText = "Keine Daten geladen";
        TabellensummeText = "Keine Tabellensumme geladen.";
        NaechsteSeiteCommand.NotifyCanExecuteChanged();
        VorherigeSeiteCommand.NotifyCanExecuteChanged();
        StatusText = "Tabellenfilter geändert – bitte aktualisieren.";
    }

    private void FilterGeaendert()
    {
        if (MerkeInlineFilterAenderung(nurTabelle: false)) return;
        Interlocked.Increment(ref _generation);
        LeereAnzeige();
        StatusText = "Filter geändert – bitte aktualisieren.";
    }

    private void LeereAnzeige()
    {
        _perioden.Clear();
        Jahresrekorde.Clear();
        Einzelrekorde.Clear();
        Jahresziele.Clear();
        AusgewaehlteTour = null;
        Diagramm = new PlotModel { Background = OxyColors.White };
        DistanzKmText = HoehenmeterText = FahrzeitText = AnzahlText = "–";
        _saisonTexte = ApiSaisonTexte.Leer;
        SaisonText = string.Empty;
        ZukunftsHinweis = string.Empty;
        Touren.Clear();
        GesamtTouren = 0;
        _start = 0;
        SeitenText = "Keine Daten geladen";
        TabellensummeText = "Keine Tabellensumme geladen.";
        LeerePulsanzeige();
        NaechsteSeiteCommand.NotifyCanExecuteChanged();
        VorherigeSeiteCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(KannAktualisieren))]
    public async Task AktualisierenAsync(CancellationToken ct = default)
    {
        if (_inlineBeendet || Laedt || InlineGesperrt) return;
        VerwirfInlineEntwurf();
        _inlineLookups = null;
        var generation = Interlocked.Increment(ref _generation);
        var vonSnapshot = VonDatum;
        var bisSnapshot = BisDatum;
        var rasterSnapshot = Raster;
        var tabellenfilterSnapshot = Tabellenfilter.ErstelleAnfrage();
        var vorherAktivitaetId = AktivitaetAuswahl?.Id;
        var vorherKategorieId = KategorieAuswahl?.Id;
        var initial = !_lookupsGeladen;
        Laedt = true;
        StatusText = "Filter und Stammdaten werden geladen …";
        try
        {
            if (vonSnapshot is null || bisSnapshot is null || vonSnapshot > bisSnapshot)
            {
                StatusText = "Bitte einen gültigen Zeitraum auswählen.";
                return;
            }

            var plan = await LadeFilterOptionenAsync(generation, vorherAktivitaetId, vorherKategorieId, initial, ct);
            if (plan is null || generation != Interlocked.Read(ref _generation)) return;

            var von = DateOnly.FromDateTime(vonSnapshot.Value);
            var bis = DateOnly.FromDateTime(bisSnapshot.Value);
            var aktivitaetId = plan.Aktivitaet.Id;
            var kategorieId = plan.Kategorie.Id;
            var request = new AktivitaetsauswertungAnfrage(von, bis, rasterSnapshot, aktivitaetId, kategorieId);
            var filter = new TourenListenfilter(von, bis, aktivitaetId, kategorieId)
            { Sortierung = TourenSortierung, Absteigend = TourenAbsteigend, Tabellenfilter = tabellenfilterSnapshot };
            var auswertungTask = _auswertung.Laden(request, ct);
            var tourenTask = _touren.ListeGefiltert(0, 50, filter, ct);
            await Task.WhenAll(auswertungTask, tourenTask);
            ct.ThrowIfCancellationRequested();
            if (generation != Interlocked.Read(ref _generation)) return;

            var result = await auswertungTask;
            var page = await tourenTask;
            ApiTourSeitenRegeln.Validieren(page, 0, 50);
            var tabellenfilterPlan = ApiTabellenfilterOptionenPlan.Erstellen(page.TabellenfilterOptionen,
                tabellenfilterSnapshot?.EquipmentName, tabellenfilterSnapshot?.Trainingsbereich);
            var tabellensummeText = ApiTourTabellensummeAnzeige.Erstellen(page);
            var saisonTexte = ApiSaisonAnzeige.Erstellen(result.Saisonfortschritt, von, bis);

            if (plan.AuswahlGeaendert) LeereAnzeige();
            var selectionFilterChanged = false;
            UebernehmeFilterOptionen(plan);
            Tabellenfilter.Uebernehmen(tabellenfilterPlan);
            _start = 0;

            _perioden.Clear();
            foreach (var periode in result.Perioden) _perioden.Add(periode);
            Jahresrekorde.Clear(); foreach (var row in result.Jahresrekorde) Jahresrekorde.Add(row);
            Einzelrekorde.Clear(); foreach (var row in result.Einzelrekorde) Einzelrekorde.Add(row);
            Jahresziele.Clear(); foreach (var row in result.Jahresziele) Jahresziele.Add(row);
            DistanzKmText = $"{result.DistanzKm.ToString("N1", Deutsch)} km";
            HoehenmeterText = $"{result.Hoehenmeter.ToString("N0", Deutsch)} hm";
            FahrzeitText = DauerText(result.FahrzeitSekunden);
            AnzahlText = result.Anzahl.ToString("N0", Deutsch);
            ZukunftsHinweis = result.ZukuenftigeAktivitaeten > 0
                ? $"{result.ZukuenftigeAktivitaeten} zukünftige Aktivität(en) im Datenbestand."
                : string.Empty;
            selectionFilterChanged = UebernehmeAuswahlQuery(filter);
            SetzeSeite(page);
            TabellensummeText = tabellensummeText;
            _saisonTexte = saisonTexte;
            AktualisiereSaisonText();
            ZeichneDiagramm();
            StatusText = selectionFilterChanged
                ? "Filter geändert – die manuelle Tourenauswahl wurde aufgehoben."
                : plan.Hinweis ?? (page.Gesamt == 0 ? "Keine Touren im gewählten Zeitraum und Filter." : string.Empty);
            Interlocked.Increment(ref _aktualisierungsstand);
            OnPropertyChanged(nameof(Aktualisierungsstand));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (generation == Interlocked.Read(ref _generation)) StatusText = "Ladevorgang abgebrochen.";
        }
        catch (ApiTourSeitenException)
        {
            if (generation == Interlocked.Read(ref _generation) && !ct.IsCancellationRequested)
                StatusText = "Tourenseite ist unvollständig oder ungültig. Bitte Backend-Version und Verbindung prüfen.";
        }
        catch (ApiSaisonAntwortException)
        {
            if (generation == Interlocked.Read(ref _generation) && !ct.IsCancellationRequested)
                StatusText = "Saisonkennzahlen sind unvollständig oder ungültig.";
        }
        catch (Exception)
        {
            if (generation == Interlocked.Read(ref _generation) && !ct.IsCancellationRequested)
                StatusText = "Backenddaten konnten nicht geladen werden.";
        }
        finally { if (!_inlineBeendet) Laedt = false; }
    }

    private async Task<ApiFilterOptionenPlan?> LadeFilterOptionenAsync(long generation,
        long? vorherAktivitaetId, long? vorherKategorieId, bool initial, CancellationToken ct)
    {
        var stammdaten = await _stammdatenLeser.Laden(ct);
        ct.ThrowIfCancellationRequested();
        if (generation != Interlocked.Read(ref _generation)) return null;
        return ApiFilterOptionenPlan.Erstellen(stammdaten, vorherAktivitaetId, vorherKategorieId, initial);
    }

    private void UebernehmeFilterOptionen(ApiFilterOptionenPlan plan)
    {
        _filterAenderungSuspendiert = true;
        try
        {
            AktivitaetsOptionen.Clear();
            foreach (var option in plan.Aktivitaeten) AktivitaetsOptionen.Add(option);
            KategorieOptionen.Clear();
            foreach (var option in plan.Kategorien) KategorieOptionen.Add(option);
            AktivitaetAuswahl = plan.Aktivitaet;
            KategorieAuswahl = plan.Kategorie;
        }
        finally { _filterAenderungSuspendiert = false; }

        _lookupsGeladen = true;
        OnPropertyChanged(nameof(AktivitaetsOptionen));
        OnPropertyChanged(nameof(KategorieOptionen));
    }

    [RelayCommand(CanExecute = nameof(KannNaechsteSeite))]
    public async Task NaechsteSeiteAsync(CancellationToken ct = default)
    {
        if (_start + 50 >= GesamtTouren) return;
        await LadeSeiteAsync(_start + 50, ct);
    }

    [RelayCommand(CanExecute = nameof(KannVorherigeSeite))]
    public async Task VorherigeSeiteAsync(CancellationToken ct = default)
    {
        if (_start == 0) return;
        await LadeSeiteAsync(Math.Max(0, _start - 50), ct);
    }

    private bool KannAktualisieren() => !Laedt && !InlineGesperrt;
    partial void OnLaedtChanged(bool value)
    {
        OnPropertyChanged(nameof(FilterControlsEnabled));
        OnPropertyChanged(nameof(KannAlleFiltertrefferAuswaehlen));
        AktualisierenCommand.NotifyCanExecuteChanged();
        NaechsteSeiteCommand.NotifyCanExecuteChanged();
        VorherigeSeiteCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HatTourenauswahl));
        OnPropertyChanged(nameof(AuswahlAnzahl));
        BenachrichtigeTourCommands();
        BenachrichtigePulsCommands();
    }

    private bool KannNaechsteSeite() => !Laedt && !InlineGesperrt && _start + 50 < GesamtTouren;
    private bool KannVorherigeSeite() => !Laedt && !InlineGesperrt && _start > 0;

    private async Task LadeSeiteAsync(int start, CancellationToken ct)
    {
        if (_inlineBeendet || Laedt || InlineGesperrt || VonDatum is null || BisDatum is null || VonDatum > BisDatum) return;
        var tabellenfilterSnapshot = Tabellenfilter.ErstelleAnfrage();
        var filter = new TourenListenfilter(DateOnly.FromDateTime(VonDatum.Value),
            DateOnly.FromDateTime(BisDatum.Value), AktivitaetAuswahl?.Id, KategorieAuswahl?.Id)
        { Sortierung = TourenSortierung, Absteigend = TourenAbsteigend, Tabellenfilter = tabellenfilterSnapshot };
        var generation = Interlocked.Read(ref _generation);
        Laedt = true;
        try
        {
            var page = await _touren.ListeGefiltert(start, 50, filter, ct);
            ct.ThrowIfCancellationRequested();
            if (generation != Interlocked.Read(ref _generation)) return;
            ApiTourSeitenRegeln.Validieren(page, start, 50);
            var tablePlan = ApiTabellenfilterOptionenPlan.Erstellen(page.TabellenfilterOptionen,
                tabellenfilterSnapshot?.EquipmentName, tabellenfilterSnapshot?.Trainingsbereich);
            var tabellensummeText = ApiTourTabellensummeAnzeige.Erstellen(page);
            Tabellenfilter.Uebernehmen(tablePlan);
            _start = start;
            var selectionFilterChanged = UebernehmeAuswahlQuery(filter);
            SetzeSeite(page);
            TabellensummeText = tabellensummeText;
            StatusText = selectionFilterChanged
                ? "Filter geändert – die manuelle Tourenauswahl wurde aufgehoben."
                : string.Empty;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (generation == Interlocked.Read(ref _generation)) StatusText = "Ladevorgang abgebrochen.";
        }
        catch (ApiTourSeitenException)
        {
            if (generation == Interlocked.Read(ref _generation) && !ct.IsCancellationRequested)
                StatusText = "Tourenseite ist unvollständig oder ungültig. Bitte Backend-Version und Verbindung prüfen.";
        }
        catch (Exception) { if (generation == Interlocked.Read(ref _generation) && !ct.IsCancellationRequested) StatusText = "Tourenseite konnte nicht geladen werden."; }
        finally { if (!_inlineBeendet) Laedt = false; }
    }

    private bool UebernehmeAuswahlQuery(TourenListenfilter filter)
    {
        var key = FormatiereAuswahlQuery(filter);
        var changed = _angewandteAuswahlQuery is not null && _angewandteAuswahlQuery != key;
        if (changed) HebeTourenauswahlAuf();
        _angewandteAuswahlQuery = key;
        _angewandterFilterauswahlSnapshot = Auswahlfilter(filter);
        OnPropertyChanged(nameof(KannAlleFiltertrefferAuswaehlen));
        return changed;
    }

    private static string FormatiereAuswahlQuery(TourenListenfilter filter) =>
        System.Text.Json.JsonSerializer.Serialize(new { filter.Von, filter.Bis, filter.AktivitaetId, filter.KategorieId, filter.Tabellenfilter });

    private void SetzeSeite(TourListeAntwort page)
    {
        InlineNeuLadenNoetig = false;
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
        var model = new PlotModel
        {
            Culture = Deutsch,
            Background = OxyColors.White,
            PlotAreaBorderColor = OxyColor.FromRgb(194, 201, 209)
        };
        var zeitachse = new CategoryAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = -0.5,
            Maximum = _perioden.Count - 0.5,
            IsPanEnabled = false,
            IsZoomEnabled = false,
            Angle = _perioden.Count > 12 ? -45 : 0,
            MajorStep = Math.Max(1, (int)Math.Ceiling(_perioden.Count / 12d)),
            TextColor = OxyColor.FromRgb(70, 86, 106),
            AxislineColor = OxyColor.FromRgb(143, 152, 163),
            MajorGridlineStyle = LineStyle.None
        };
        var einheit = Kennzahl switch
        {
            ApiDiagrammkennzahl.Kilometer => "km",
            ApiDiagrammkennzahl.Hoehenmeter => "hm",
            ApiDiagrammkennzahl.Fahrzeit => "Stunden",
            _ => "Touren"
        };
        model.Axes.Add(zeitachse);
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Minimum = 0, Title = einheit, IsPanEnabled = false, IsZoomEnabled = false, TextColor = OxyColor.FromRgb(70, 86, 106), AxislineColor = OxyColor.FromRgb(143, 152, 163), MajorGridlineStyle = LineStyle.Solid, MajorGridlineColor = OxyColor.FromRgb(232, 236, 240), MajorGridlineThickness = 1 });
        var bars = new RectangleBarSeries { FillColor = OxyColor.FromRgb(36, 109, 178), StrokeThickness = 0 };
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

public enum ApiDiagrammkennzahl { Kilometer, Hoehenmeter, Fahrzeit, Anzahl }
