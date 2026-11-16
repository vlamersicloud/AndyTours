using CommunityToolkit.Mvvm.Input;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiOverlaysViewModel
{
    partial void OnGewaehlteFarbeChanged(ApiOverlayFarboption? value)
    {
        var persisted = Ausgewaehlt?.Overlay?.Farbe;
        if (!_wirdAngezeigt && value?.Farbe != persisted && KannOverlayAendern)
            EigeneFarbeAendernCommand.Execute(value);
    }

    partial void OnGewaehlteGemeinsameFarbeChanged(ApiOverlayFarboption? value)
    {
        if (!_wirdAngezeigt && value?.Farbe != BestehenderBestand?.GemeinsameFarbe && KannAendern)
            GemeinsameFarbeAendernCommand.Execute(value);
    }

    [RelayCommand(CanExecute = nameof(KannOverlayAendern))]
    private async Task EigeneFarbeAendern(ApiOverlayFarboption? option, CancellationToken cancellationToken)
    {
        if (_beendet || !KannOverlayAendern || Ausgewaehlt?.Overlay is not { } overlay || BestehenderBestand is not { } bestand ||
            option is null || (option.Farbe is { } color && !Enum.IsDefined(color))) return;
        var id = overlay.Id;
        var request = new OverlayAendernAnfrage(bestand.Version, overlay.OrdnerId, overlay.Name, option.Farbe);
        var previous = OptionEigene(overlay.Farbe, bestand.GemeinsameFarbe);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lebensdauerToken);
        CancellationToken ct = linked.Token;
        Aendert = true;
        try
        {
            await _verwaltung.OverlayAendern(id, request, ct);
            ct.ThrowIfCancellationRequested();
            if (_beendet) return;
            var fresh = await AktualisiereNachSchreibenAsync(ct);
            if (fresh is null) { Hinweis = "Farbänderung bestätigt, aktueller Bestand konnte aber nicht neu geladen werden."; return; }
            Hinweis = _kontext.LastHinweis;
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
        var previous = ApiOverlayFarboptionen.Alle().FirstOrDefault(x => x.Farbe == bestand.GemeinsameFarbe);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lebensdauerToken);
        CancellationToken ct = linked.Token;
        Aendert = true;
        try
        {
            await _verwaltung.GemeinsameFarbe(new OverlayGemeinsameFarbeAnfrage(bestand.Version, color), ct);
            ct.ThrowIfCancellationRequested();
            if (_beendet) return;
            var fresh = await AktualisiereNachSchreibenAsync(ct);
            if (fresh is null) { Hinweis = "Gemeinsame Farbe bestätigt, aktueller Bestand konnte aber nicht neu geladen werden."; return; }
            Hinweis = _kontext.LastHinweis;
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
        if (_beendet) return;
        _wirdAngezeigt = true;
        try { GewaehlteFarbe = value; }
        finally { _wirdAngezeigt = false; }
    }

    private void RestoreCommon(ApiOverlayFarboption? value)
    {
        if (_beendet) return;
        _wirdAngezeigt = true;
        try { GewaehlteGemeinsameFarbe = value; }
        finally { _wirdAngezeigt = false; }
    }

    private static ApiOverlayFarboption? OptionEigene(OverlayFarbe? own, OverlayFarbe common) => own is { } value
        ? ApiOverlayFarboptionen.Alle().FirstOrDefault(option => option.Farbe == value)
        : ApiOverlayFarboptionen.Gemeinsam(common);
}
