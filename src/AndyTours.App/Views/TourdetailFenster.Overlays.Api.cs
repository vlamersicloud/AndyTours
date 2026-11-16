using Mapsui.Layers;
using Mapsui;
using AndyTours.App.ViewModels;
using AndyTours.App.Funktionen;
using AndyTours.Core.Modell;

namespace AndyTours.App.Views;

public partial class TourdetailFenster
{
    private readonly ApiOverlayKontext? _apiOverlayKontext;

    private void FuelleOverlays(Map karte)
    {
        if (_apiOverlayKontext is null && DataContext is not TourdetailViewModel { IstApiModus: true })
        {
            Overlaydarstellung.Fuelle(karte);
            return;
        }

        var state = _apiOverlayKontext?.Anzeige;
        if (state is null)
        {
            if (karte.Layers.FirstOrDefault(layer => layer.Name == "Overlays") is MemoryLayer overlayLayer)
            {
                overlayLayer.Features = [];
                karte.Refresh();
            }
            return;
        }
        Overlaydarstellung.Fuelle(karte, state.Eingeschaltete, state.Farbe);
    }

    private void ApiOverlaysGeaendert(object? sender, EventArgs e) => FuelleOverlays(Karte.Map);

    private void ApiGanzesOverlayAngefordert(object? sender, Overlay overlay) => Overlaydarstellung.ZeigeGanz(Karte.Map, overlay);
}
