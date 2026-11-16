using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;

namespace AndyTours.App.ViewModels;

/// <summary>Lokaler, noch nicht übermittelter Zustand der tabellenspezifischen Tourenfilter.</summary>
public partial class ApiTourTabellenfilterViewModel : ObservableObject
{
    private bool _aenderungenUnterdrueckt;
    private ApiTabellenfilterOption _ausgewaehltesEquipment = Alle("Alle Equipments");
    private ApiTabellenfilterOption _ausgewaehlterBereich = Alle("Alle Trainingsbereiche");
    private string _distanzVon = string.Empty;
    private string _distanzBis = string.Empty;
    private string _hoehenmeterVon = string.Empty;
    private string _hoehenmeterBis = string.Empty;
    private string _suchbegriff = string.Empty;

    public ApiTourTabellenfilterViewModel()
    {
        Equipments.Add(_ausgewaehltesEquipment);
        Trainingsbereiche.Add(_ausgewaehlterBereich);
        ResetZuruecksetzenCommand = new RelayCommand(Zuruecksetzen, () => IstGesetzt);
    }

    public ObservableCollection<ApiTabellenfilterOption> Equipments { get; } = [];
    public ObservableCollection<ApiTabellenfilterOption> Trainingsbereiche { get; } = [];

    public ApiTabellenfilterOption AusgewaehltesEquipment
    {
        get => _ausgewaehltesEquipment;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_ausgewaehltesEquipment, value)) return;
            _ausgewaehltesEquipment = value;
            EingabeGeaendert(nameof(AusgewaehltesEquipment), notifyOwn: true);
        }
    }

    public ApiTabellenfilterOption AusgewaehlterBereich
    {
        get => _ausgewaehlterBereich;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_ausgewaehlterBereich, value)) return;
            _ausgewaehlterBereich = value;
            EingabeGeaendert(nameof(AusgewaehlterBereich), notifyOwn: true);
        }
    }

    public string DistanzVon { get => _distanzVon; set => SetzeText(ref _distanzVon, value, nameof(DistanzVon)); }
    public string DistanzBis { get => _distanzBis; set => SetzeText(ref _distanzBis, value, nameof(DistanzBis)); }
    public string HoehenmeterVon { get => _hoehenmeterVon; set => SetzeText(ref _hoehenmeterVon, value, nameof(HoehenmeterVon)); }
    public string HoehenmeterBis { get => _hoehenmeterBis; set => SetzeText(ref _hoehenmeterBis, value, nameof(HoehenmeterBis)); }
    public string Suchbegriff { get => _suchbegriff; set => SetzeText(ref _suchbegriff, value, nameof(Suchbegriff)); }

    public bool IstGesetzt =>
        AusgewaehltesEquipment.Wert is not null || AusgewaehlterBereich.Wert is not null ||
        !string.IsNullOrWhiteSpace(DistanzVon) || !string.IsNullOrWhiteSpace(DistanzBis) ||
        !string.IsNullOrWhiteSpace(HoehenmeterVon) || !string.IsNullOrWhiteSpace(HoehenmeterBis) ||
        !string.IsNullOrWhiteSpace(Suchbegriff);

    public Filterbereich Distanzbereich => LeseFilterbereich.Ausfuehren(DistanzVon, DistanzBis);
    public Filterbereich Hoehenmeterbereich => LeseFilterbereich.Ausfuehren(HoehenmeterVon, HoehenmeterBis);
    public bool DistanzVonFehlerhaft => Distanzbereich.VonFehlerhaft;
    public bool DistanzBisFehlerhaft => Distanzbereich.BisFehlerhaft;
    public bool HoehenmeterVonFehlerhaft => Hoehenmeterbereich.VonFehlerhaft;
    public bool HoehenmeterBisFehlerhaft => Hoehenmeterbereich.BisFehlerhaft;
    public bool DistanzbereichUngueltig => !Distanzbereich.IstGueltig;
    public bool HoehenmeterbereichUngueltig => !Hoehenmeterbereich.IstGueltig;

    public event Action? Geaendert;

    public TourenTabellenfilter? ErstelleAnfrage() => IstGesetzt
        ? new TourenTabellenfilter(
            AusgewaehltesEquipment.Wert,
            AusgewaehlterBereich.Wert,
            DistanzVon,
            DistanzBis,
            HoehenmeterVon,
            HoehenmeterBis,
            Suchbegriff)
        : null;

    public void Uebernehmen(ApiTabellenfilterOptionenPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var vorher = _aenderungenUnterdrueckt;
        _aenderungenUnterdrueckt = true;
        try
        {
            Replace(Equipments, plan.Equipments);
            Replace(Trainingsbereiche, plan.Trainingsbereiche);
            _ausgewaehltesEquipment = plan.AusgewaehltesEquipment;
            _ausgewaehlterBereich = plan.AusgewaehlterBereich;
            OnPropertyChanged(nameof(AusgewaehltesEquipment));
            OnPropertyChanged(nameof(AusgewaehlterBereich));
            OnPropertyChanged(nameof(IstGesetzt));
            OnPropertyChanged(nameof(DistanzVonFehlerhaft));
            OnPropertyChanged(nameof(DistanzBisFehlerhaft));
            OnPropertyChanged(nameof(HoehenmeterVonFehlerhaft));
            OnPropertyChanged(nameof(HoehenmeterBisFehlerhaft));
            OnPropertyChanged(nameof(DistanzbereichUngueltig));
            OnPropertyChanged(nameof(HoehenmeterbereichUngueltig));
            ResetZuruecksetzenCommand.NotifyCanExecuteChanged();
        }
        finally { _aenderungenUnterdrueckt = vorher; }
    }

    public RelayCommand ResetZuruecksetzenCommand { get; }

    private void Zuruecksetzen()
    {
        var vorher = _aenderungenUnterdrueckt;
        _aenderungenUnterdrueckt = true;
        try
        {
            DistanzVon = string.Empty;
            DistanzBis = string.Empty;
            HoehenmeterVon = string.Empty;
            HoehenmeterBis = string.Empty;
            Suchbegriff = string.Empty;
            _ausgewaehltesEquipment = Equipments[0];
            _ausgewaehlterBereich = Trainingsbereiche[0];
            OnPropertyChanged(nameof(AusgewaehltesEquipment));
            OnPropertyChanged(nameof(AusgewaehlterBereich));
        }
        finally { _aenderungenUnterdrueckt = vorher; }
        OnPropertyChanged(nameof(IstGesetzt));
        OnPropertyChanged(nameof(DistanzVonFehlerhaft));
        OnPropertyChanged(nameof(DistanzBisFehlerhaft));
        OnPropertyChanged(nameof(HoehenmeterVonFehlerhaft));
        OnPropertyChanged(nameof(HoehenmeterBisFehlerhaft));
        OnPropertyChanged(nameof(DistanzbereichUngueltig));
        OnPropertyChanged(nameof(HoehenmeterbereichUngueltig));
        ResetZuruecksetzenCommand.NotifyCanExecuteChanged();
        if (!vorher) Geaendert?.Invoke();
    }

    private void SetzeText(ref string field, string value, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref field, value, propertyName)) EingabeGeaendert(propertyName);
    }

    private void EingabeGeaendert(string propertyName, bool notifyOwn = false)
    {
        if (notifyOwn) OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(IstGesetzt));
        if (propertyName.StartsWith("Distanz", StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(DistanzVonFehlerhaft));
            OnPropertyChanged(nameof(DistanzBisFehlerhaft));
            OnPropertyChanged(nameof(DistanzbereichUngueltig));
        }
        if (propertyName.StartsWith("Hoehenmeter", StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(HoehenmeterVonFehlerhaft));
            OnPropertyChanged(nameof(HoehenmeterBisFehlerhaft));
            OnPropertyChanged(nameof(HoehenmeterbereichUngueltig));
        }
        ResetZuruecksetzenCommand.NotifyCanExecuteChanged();
        if (!_aenderungenUnterdrueckt) Geaendert?.Invoke();
    }

    private static void Replace(ObservableCollection<ApiTabellenfilterOption> target, IEnumerable<ApiTabellenfilterOption> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private static ApiTabellenfilterOption Alle(string label) => new(null, label);
}
