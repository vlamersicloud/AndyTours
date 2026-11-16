using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AndyTours.Application.Overlays;
using AndyTours.App.Funktionen;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiOverlaysViewModel : ObservableObject, IDisposable
{
    private readonly ApiOverlayKontext _kontext;
    private readonly IOverlayVerwaltung _verwaltung;
    private readonly IOverlayGpxImport _gpxImport;
    private readonly Func<string, bool> _bestaetigen;
    private readonly CancellationTokenSource _lebensdauer = new();
    private readonly CancellationToken _lebensdauerToken;
    private bool _beendet;
    private bool _baumGueltig = true;

    public ApiOverlaysViewModel(ApiOverlayKontext kontext, IOverlayVerwaltung verwaltung, IOverlayGpxImport gpxImport, Func<string, bool> bestaetigen)
    {
        _kontext = kontext ?? throw new ArgumentNullException(nameof(kontext));
        _verwaltung = verwaltung ?? throw new ArgumentNullException(nameof(verwaltung));
        _gpxImport = gpxImport ?? throw new ArgumentNullException(nameof(gpxImport));
        _bestaetigen = bestaetigen ?? throw new ArgumentNullException(nameof(bestaetigen));
        _lebensdauerToken = _lebensdauer.Token;
        _kontext.Geaendert += KontextGeaendert;
        if (_kontext.Anzeige is not null) KontextGeaendert(_kontext, EventArgs.Empty);
    }

    public ObservableCollection<ApiOverlayknoten> Wurzeln { get; } = [];
    public OverlayBestandAntwort? BestehenderBestand => _kontext.Anzeige?.Bestand;
    public bool IstBereit => !_beendet && !IstBeschaeftigt && _baumGueltig && BestehenderBestand is not null;
    public bool IstEtwasAusgewaehlt => Ausgewaehlt is not null;
    public bool IstOverlayAusgewaehlt => Ausgewaehlt?.IstOverlay == true;
    public bool IstBeschaeftigt => Laedt || Aendert;
    public bool IstLeer => BestehenderBestand is { Overlays.Count: 0 };
    public string LeerhinweisText => "Noch keine Overlays vorhanden. Mit „GPX hinzufügen…“ eine oder mehrere Dateien aufnehmen.";
    public ObservableCollection<ApiOverlayFarboption> Farbauswahl { get; } = [];
    public ObservableCollection<ApiOverlayFarboption> GemeinsameFarbauswahl { get; } = [];
    public event EventHandler? Schliessen;
    public event EventHandler? NameBearbeiten;
    public bool KannAendern => IstBereit && Ausgewaehlt is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IstEtwasAusgewaehlt), nameof(IstOverlayAusgewaehlt), nameof(KannAendern))]
    private ApiOverlayknoten? _ausgewaehlt;
    [ObservableProperty] private string _bearbeitungsname = string.Empty;
    [ObservableProperty] private string _hinweis = string.Empty;
    [ObservableProperty] private bool _laedt;
    [ObservableProperty] private bool _aendert;
    [ObservableProperty] private ApiOverlayFarboption? _gewaehlteFarbe;
    [ObservableProperty] private ApiOverlayFarboption? _gewaehlteGemeinsameFarbe;

    public void Dispose()
    {
        if (_beendet) return;
        _beendet = true;
        _kontext.Geaendert -= KontextGeaendert;
        _lebensdauer.Cancel();
        Laedt = false; Aendert = false;
        Wurzeln.Clear(); Farbauswahl.Clear(); GemeinsameFarbauswahl.Clear();
        Ausgewaehlt = null; Schliessen = null; NameBearbeiten = null;
        _lebensdauer.Dispose();
        OnPropertyChanged(nameof(IstBereit)); OnPropertyChanged(nameof(KannAendern));
    }

    public void FordereNamensbearbeitungAn() => NameBearbeiten?.Invoke(this, EventArgs.Empty);
    public void FordereSchliessenAn() => Schliessen?.Invoke(this, EventArgs.Empty);

    partial void OnLaedtChanged(bool value) => ArbeitszustandGeaendert();
    partial void OnAendertChanged(bool value) => ArbeitszustandGeaendert();
    partial void OnAusgewaehltChanged(ApiOverlayknoten? value)
    {
        Bearbeitungsname = value?.Name ?? string.Empty;
        AuswahlAnzeigen(value);
        OnPropertyChanged(nameof(KannAendern));
        BenachrichtigeBefehle();
    }

    private void ArbeitszustandGeaendert()
    {
        OnPropertyChanged(nameof(IstBeschaeftigt)); OnPropertyChanged(nameof(IstBereit)); OnPropertyChanged(nameof(KannAendern));
        BenachrichtigeBefehle();
    }

    private void KontextGeaendert(object? sender, EventArgs e)
    {
        if (_beendet) return;
        UebernimmAnzeige();
        OnPropertyChanged(nameof(BestehenderBestand)); OnPropertyChanged(nameof(IstBereit)); OnPropertyChanged(nameof(IstLeer)); OnPropertyChanged(nameof(KannAendern));
        BenachrichtigeBefehle();
    }

    partial void UebernimmAnzeige();
    partial void AuswahlAnzeigen(ApiOverlayknoten? value);
    partial void BenachrichtigeBefehle();
}
