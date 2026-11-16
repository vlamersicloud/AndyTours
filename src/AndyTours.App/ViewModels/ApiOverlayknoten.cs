using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

/// <summary>Pure API-contract tree node; carries no WPF or persistence objects.</summary>
public sealed partial class ApiOverlayknoten : ObservableObject
{
    private readonly Action<ApiOverlayknoten, bool> _umschalten;
    private bool? _istEingeschaltet;

    public ApiOverlayknoten(OverlayOrdnerAntwort ordner, Action<ApiOverlayknoten, bool> umschalten)
    {
        Ordner = ordner ?? throw new ArgumentNullException(nameof(ordner));
        _umschalten = umschalten ?? throw new ArgumentNullException(nameof(umschalten));
    }

    public ApiOverlayknoten(OverlayMetadatenAntwort overlay, string farbHex, Action<ApiOverlayknoten, bool> umschalten)
    {
        Overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        Farbe = farbHex;
        _istEingeschaltet = overlay.IstEingeschaltet;
        _umschalten = umschalten ?? throw new ArgumentNullException(nameof(umschalten));
    }

    public OverlayOrdnerAntwort? Ordner { get; }
    public OverlayMetadatenAntwort? Overlay { get; private set; }
    public long Id => Ordner?.Id ?? Overlay!.Id;
    public bool IstOrdner => Ordner is not null;
    public bool IstOverlay => Overlay is not null;
    public string Name => Ordner?.Name ?? Overlay!.Name;
    public ObservableCollection<ApiOverlayknoten> Kinder { get; } = [];

    [ObservableProperty] private string? _farbe;
    [ObservableProperty] private bool _istAufgeklappt;
    [ObservableProperty] private bool _istAusgewaehlt;

    public event EventHandler? Ausgewaehlt;

    public bool? IstEingeschaltet
    {
        get => _istEingeschaltet;
        set
        {
            if (value != _istEingeschaltet) _umschalten(this, value == true);
        }
    }

    public void ZeigeZustand(bool? zustand) => SetProperty(ref _istEingeschaltet, zustand, nameof(IstEingeschaltet));

    public void Aktualisiere(OverlayMetadatenAntwort overlay, string farbHex)
    {
        if (!IstOverlay || overlay.Id != Id) throw new ArgumentException("Overlay-Knoten passt nicht.", nameof(overlay));
        Overlay = overlay;
        OnPropertyChanged(nameof(Name));
        Farbe = farbHex;
        ZeigeZustand(overlay.IstEingeschaltet);
    }

    partial void OnIstAusgewaehltChanged(bool value)
    {
        if (value) Ausgewaehlt?.Invoke(this, EventArgs.Empty);
    }
}
