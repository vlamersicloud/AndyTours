using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiOverlaysViewModel
{
    [RelayCommand(CanExecute = nameof(KannZeigeGanz))]
    private async Task ZeigeGanz(ApiOverlayknoten? node, CancellationToken cancellationToken)
    {
        if (_beendet || IstBeschaeftigt || node is null || !IstAktuellerOverlayknoten(node)) return;
        long id = node.Id;
        if (node.Overlay?.IstEingeschaltet != true)
        {
            try { await Umschalten(new UmschaltAnfrage(node, true), cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            if (_beendet || cancellationToken.IsCancellationRequested) return;
        }
        var state = _kontext.Anzeige;
        if (state?.Bestand.Overlays.Any(o => o.Id == id && o.IstEingeschaltet) == true &&
            state.Eingeschaltete.Any(o => o.Id == id))
            _kontext.ZeigeGanz(id);
    }

    private bool KannZeigeGanz() => !_beendet && IstBereit && !IstBeschaeftigt;
    private bool IstAktuellerOverlayknoten(ApiOverlayknoten node) =>
        node.IstOverlay && Wurzeln.SelectMany(Flatten).Any(current => ReferenceEquals(current, node));

    partial void BenachrichtigeZoomBefehle();
}
