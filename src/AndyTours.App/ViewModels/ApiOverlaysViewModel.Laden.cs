using CommunityToolkit.Mvvm.Input;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiOverlaysViewModel
{
    public sealed record UmschaltAnfrage(ApiOverlayknoten Knoten, bool Einschalten);

    [RelayCommand(CanExecute = nameof(KannLaden))]
    private async Task Laden(CancellationToken cancellationToken)
    {
        if (_beendet || IstBeschaeftigt) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lebensdauerToken);
        CancellationToken ct = linked.Token;
        Laedt = true;
        try
        {
            await _kontext.LadenAsync(ct);
            ct.ThrowIfCancellationRequested();
            if (!_beendet) Hinweis = _kontext.LastHinweis;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception)
        {
            if (!_beendet) Hinweis = "Overlay-Bestand konnte nicht geladen werden. Bitte Verbindung prüfen.";
        }
        finally { if (!_beendet) Laedt = false; }
    }

    [RelayCommand(CanExecute = nameof(KannAendern))]
    private async Task Umschalten(UmschaltAnfrage request, CancellationToken cancellationToken)
    {
        if (_beendet || !KannAendern || IstBeschaeftigt || BestehenderBestand is not { } bestand ||
            request is null || !Wurzeln.SelectMany(Flatten).Any(node => ReferenceEquals(node, request.Knoten))) return;
        var ids = ApiOverlayBaum.OverlayIds(request.Knoten);
        if (ids.Count == 0)
        {
            if (Wurzeln.SelectMany(Flatten).FirstOrDefault(n => n.Id == request.Knoten.Id && n.IstOrdner) is { } empty)
                empty.ZeigeZustand(false);
            return;
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lebensdauerToken);
        CancellationToken ct = linked.Token;
        Aendert = true;
        try
        {
            await _verwaltung.Sichtbarkeit(new OverlaySichtbarkeitAnfrage(bestand.Version, ids, request.Einschalten), ct);
            ct.ThrowIfCancellationRequested();
            await _kontext.LadenAsync(ct);
            ct.ThrowIfCancellationRequested();
            if (!_beendet) Hinweis = _kontext.LastHinweis;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (StammdatenException ex) when (!_beendet && ex.Art == StammdatenFehlerart.Konflikt)
        {
            Hinweis = "Overlay-Bestand wurde geändert. Bitte neu laden; der Zustand wurde nicht blind erneut gespeichert.";
        }
        catch (Exception)
        {
            if (!_beendet) Hinweis = "Sichtbarkeit konnte nicht bestätigt werden. Bitte Bestand neu laden.";
        }
        finally { if (!_beendet) Aendert = false; }
    }

    private bool KannLaden() => !_beendet && !IstBeschaeftigt;

    partial void Umschaltwunsch(ApiOverlayknoten node, bool value)
    {
        if (!_beendet && KannAendern && Wurzeln.SelectMany(Flatten).Any(current => ReferenceEquals(current, node)))
            UmschaltenCommand.Execute(new UmschaltAnfrage(node, value));
    }

    partial void BenachrichtigeBefehle()
    {
        LadenCommand.NotifyCanExecuteChanged();
        UmschaltenCommand.NotifyCanExecuteChanged();
        OrdnerAnlegenCommand.NotifyCanExecuteChanged();
        NameUebernehmenCommand.NotifyCanExecuteChanged();
        LoeschenCommand.NotifyCanExecuteChanged();
        EigeneFarbeAendernCommand.NotifyCanExecuteChanged();
        GemeinsameFarbeAendernCommand.NotifyCanExecuteChanged();
        GpxHinzufuegenCommand.NotifyCanExecuteChanged();
        ZeigeGanzCommand.NotifyCanExecuteChanged();
    }
}
