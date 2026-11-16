using CommunityToolkit.Mvvm.Input;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiOverlaysViewModel
{
    partial void OnGewaehlteFarbeChanged(ApiOverlayFarboption? value)
    {
        if (!_wirdAngezeigt && value != _letzteEigeneFarbe && KannOverlayAendern)
            EigeneFarbeAendernCommand.Execute(value);
    }

    partial void OnGewaehlteGemeinsameFarbeChanged(ApiOverlayFarboption? value)
    {
        if (!_wirdAngezeigt && value != _letzteGemeinsameFarbe && KannAendern)
            GemeinsameFarbeAendernCommand.Execute(value);
    }

    private ApiOverlayFarboption? _letzteEigeneFarbe;
    private ApiOverlayFarboption? _letzteGemeinsameFarbe;

    [RelayCommand(CanExecute = nameof(KannOverlayAendern))]
    private async Task EigeneFarbeAendern(ApiOverlayFarboption? option, CancellationToken cancellationToken)
    {
        if (_beendet || !KannOverlayAendern || Ausgewaehlt?.Overlay is not { } overlay || BestehenderBestand is not { } bestand ||
            option is null || (option.Farbe is { } color && !Enum.IsDefined(color))) return;
        var id = overlay.Id;
        var request = new OverlayAendernAnfrage(bestand.Version, overlay.OrdnerId, overlay.Name, option.Farbe);
        var previous = _letzteEigeneFarbe ?? OptionEigene(overlay.Farbe, bestand.GemeinsameFarbe);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lebensdauerToken);
        CancellationToken ct = linked.Token;
        Aendert = true;
        try
        {
            await _verwaltung.OverlayAendern(id, request, ct);
            ct.ThrowIfCancellationRequested();
            if (_beendet) return;
            var fresh = await AktualisiereNachSchreibenAsync(ct);
            if (fresh is null) { Hinweis = "Farbänderung bestätigt, aktueller Bestand konnte aber nicht neu geladen werden."; RestoreOwn(previous); return; }
            _letzteEigeneFarbe = OptionEigene(fresh.Overlays.FirstOrDefault(x => x.Id == id)?.Farbe, fresh.GemeinsameFarbe);
            Hinweis = string.Empty;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { RestoreOwn(previous); }
        catch (StammdatenException ex) when (!_beendet && ex.Art is StammdatenFehlerart.Ungueltig or StammdatenFehlerart.NichtGefunden or StammdatenFehlerart.Konflikt)
        {
            RestoreOwn(previous);
            Hinweis = ex.Art == StammdatenFehlerart.Konflikt ? "Overlay-Bestand geändert. Bitte neu laden und Farbe erneut prüfen." : "Farbe konnte nicht übernommen werden.";
        }
        catch (Exception) { if (!_beendet) { RestoreOwn(previous); Hinweis = "Speicherstatus unklar. Bitte Bestand neu laden und Farbe prüfen."; } }
        finally { if (!_beendet) Aendert = false; }
    }

    [RelayCommand(CanExecute = nameof(KannAendern))]
    private async Task GemeinsameFarbeAendern(ApiOverlayFarboption? option, CancellationToken cancellationToken)
    {
        if (_beendet || !KannAendern || BestehenderBestand is not { } bestand || option?.Farbe is not { } color || !Enum.IsDefined(color)) return;
        var previous = _letzteGemeinsameFarbe ?? ApiOverlayFarboptionen.Alle().FirstOrDefault(x => x.Farbe == bestand.GemeinsameFarbe);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lebensdauerToken);
        CancellationToken ct = linked.Token;
        Aendert = true;
        try
        {
            await _verwaltung.GemeinsameFarbe(new OverlayGemeinsameFarbeAnfrage(bestand.Version, color), ct);
            ct.ThrowIfCancellationRequested();
            if (_beendet) return;
            var fresh = await AktualisiereNachSchreibenAsync(ct);
            if (fresh is null) { Hinweis = "Gemeinsame Farbe bestätigt, aktueller Bestand konnte aber nicht neu geladen werden."; RestoreCommon(previous); return; }
            _letzteGemeinsameFarbe = ApiOverlayFarboptionen.Alle().FirstOrDefault(x => x.Farbe == fresh.GemeinsameFarbe);
            Hinweis = string.Empty;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { RestoreCommon(previous); }
        catch (StammdatenException ex) when (!_beendet && ex.Art is StammdatenFehlerart.Ungueltig or StammdatenFehlerart.NichtGefunden or StammdatenFehlerart.Konflikt)
        {
            RestoreCommon(previous);
            Hinweis = ex.Art == StammdatenFehlerart.Konflikt ? "Overlay-Bestand geändert. Bitte neu laden und gemeinsame Farbe prüfen." : "Gemeinsame Farbe konnte nicht übernommen werden.";
        }
        catch (Exception) { if (!_beendet) { RestoreCommon(previous); Hinweis = "Speicherstatus unklar. Bitte Bestand neu laden und gemeinsame Farbe prüfen."; } }
        finally { if (!_beendet) Aendert = false; }
    }

    private void RestoreOwn(ApiOverlayFarboption? value)
    {
        _wirdAngezeigt = true;
        try { GewaehlteFarbe = value; _letzteEigeneFarbe = value; }
        finally { _wirdAngezeigt = false; }
    }

    private void RestoreCommon(ApiOverlayFarboption? value)
    {
        _wirdAngezeigt = true;
        try { GewaehlteGemeinsameFarbe = value; _letzteGemeinsameFarbe = value; }
        finally { _wirdAngezeigt = false; }
    }

    private static ApiOverlayFarboption? OptionEigene(OverlayFarbe? own, OverlayFarbe common) => own is { } value
        ? ApiOverlayFarboptionen.Alle().FirstOrDefault(option => option.Farbe == value)
        : ApiOverlayFarboptionen.Gemeinsam(common);
}
