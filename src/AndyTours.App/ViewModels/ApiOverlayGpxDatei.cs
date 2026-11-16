using CommunityToolkit.Mvvm.ComponentModel;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiOverlayGpxDatei : ObservableObject
{
    private readonly string _dateiname;
    internal ApiOverlayGpxDatei(string pfad, string dateiname, long? ordnerId, Guid anfrageId,
        OverlayGpxImportAnfrage? anfrage = null)
    {
        Pfad = pfad; _dateiname = dateiname; OrdnerId = ordnerId; AnfrageId = anfrageId; Anfrage = anfrage;
    }
    internal string Pfad { get; }
    internal long? OrdnerId { get; }
    internal Guid AnfrageId { get; }
    internal OverlayGpxImportAnfrage? Anfrage { get; set; }
    public string Dateiname => _dateiname;
    [ObservableProperty] private string _status = "Wartet";
    [ObservableProperty] private bool _fertig;
    [ObservableProperty] private bool _unklar;
}
