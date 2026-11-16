using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiOverlaysViewModel
{
    public Func<string, IReadOnlyList<Auswahleintrag>, Auswahleintrag?, Auswahleintrag?>? VerschiebezielWaehlen { get; set; }

    [RelayCommand(CanExecute = nameof(KannAuswahlAendern))]
    private async Task Verschieben(CancellationToken cancellationToken)
    {
        var selected = Ausgewaehlt;
        var bestand = BestehenderBestand;
        if (_beendet || IstBeschaeftigt || !KannAuswahlAendern || selected is null || bestand is null) return;
        long id = selected.Id;
        bool isFolder = selected.IstOrdner;
        string name = selected.Name;
        string version = bestand.Version;
        long? oldParent = isFolder ? selected.Ordner!.ElternId : selected.Overlay!.OrdnerId;
        OverlayFarbe? ownColor = isFolder ? null : selected.Overlay!.Farbe;
        var folders = bestand.Ordner.Select(f => new Overlayordner(f.Id, f.ElternId, f.Name)).ToArray();
        IReadOnlyList<Auswahleintrag> choices;
        try { choices = BildeOrdnerpfade.Ausfuehren(folders, isFolder ? id : null); }
        catch (Exception) { Hinweis = "Verschiebeziel konnte nicht bestimmt werden. Bitte Bestand neu laden."; return; }

        var chooser = VerschiebezielWaehlen;
        if (chooser is null) { Hinweis = "Verschiebeziel-Auswahl ist nicht verfügbar."; return; }
        Auswahleintrag? target;
        try { target = chooser(name, choices, choices.FirstOrDefault(c => c.Id == oldParent)); }
        catch (Exception) { if (!_beendet) Hinweis = "Verschieben wurde abgebrochen."; return; }
        if (_beendet || cancellationToken.IsCancellationRequested || IstBeschaeftigt ||
            !ReferenceEquals(selected, Ausgewaehlt) || !Wurzeln.SelectMany(Flatten).Any(n => ReferenceEquals(n, selected))) return;
        if (target is null || !choices.Any(c => c.Id == target.Id && c.Name == target.Name) || target.Id == oldParent) return;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lebensdauerToken);
        CancellationToken ct = linked.Token;
        Aendert = true;
        try
        {
            if (isFolder)
                await _verwaltung.OrdnerAendern(id, new OverlayOrdnerAendernAnfrage(version, target.Id, name), ct);
            else
                await _verwaltung.OverlayAendern(id, new OverlayAendernAnfrage(version, target.Id, name, ownColor), ct);
            ct.ThrowIfCancellationRequested();
            if (_beendet) return;
            var fresh = await AktualisiereNachSchreibenAsync(ct);
            if (_beendet) return;
            if (fresh is null) { Hinweis = "Verschieben bestätigt, aktueller Bestand konnte nicht neu geladen werden."; return; }
            Hinweis = _kontext.LastHinweis;
            MarkiereKnoten(id, !isFolder);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        { if (!_beendet) Hinweis = "Verschiebe-Status unklar. Bitte Bestand neu laden und Ziel prüfen."; }
        catch (StammdatenException ex) when (!_beendet && ex.Art is StammdatenFehlerart.Ungueltig or StammdatenFehlerart.NichtGefunden or StammdatenFehlerart.Konflikt)
        {
            Hinweis = ex.Art == StammdatenFehlerart.Konflikt
                ? "Overlay-Bestand wurde geändert. Bitte neu laden; Verschieben wird nicht automatisch wiederholt."
                : "Verschieben war nicht möglich. Bitte Ziel und Bestand prüfen.";
        }
        catch (Exception) { if (!_beendet) Hinweis = "Verschiebe-Status unklar. Bitte Bestand neu laden und Ziel prüfen."; }
        finally { if (!_beendet) Aendert = false; }
    }
}
