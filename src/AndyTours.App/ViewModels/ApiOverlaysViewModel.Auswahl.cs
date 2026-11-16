using CommunityToolkit.Mvvm.Input;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiOverlaysViewModel
{
    [RelayCommand(CanExecute = nameof(KannAuswahlAendern))]
    private async Task NameUebernehmen(CancellationToken cancellationToken)
    {
        if (_beendet || !KannAuswahlAendern || Ausgewaehlt is not { } selected ||
            BestehenderBestand is not { } bestand) return;
        string name = Bearbeitungsname;
        if (string.Equals(name, selected.Name, StringComparison.Ordinal))
        {
            FordereSchliessenAn();
            return;
        }
        var oldFolder = selected.Ordner?.ElternId;
        var oldOverlayFolder = selected.Overlay?.OrdnerId;
        var oldColor = selected.Overlay?.Farbe;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lebensdauerToken);
        CancellationToken ct = linked.Token;
        Aendert = true;
        try
        {
            if (selected.IstOrdner)
                await _verwaltung.OrdnerAendern(selected.Id, new OverlayOrdnerAendernAnfrage(bestand.Version, oldFolder, name), ct);
            else
                await _verwaltung.OverlayAendern(selected.Id, new OverlayAendernAnfrage(bestand.Version, oldOverlayFolder, name, oldColor), ct);
            ct.ThrowIfCancellationRequested();
            if (_beendet) return;
            var fresh = await AktualisiereNachSchreibenAsync(ct);
            if (fresh is null)
            {
                Hinweis = "Änderung bestätigt, aktueller Bestand konnte nicht neu geladen werden.";
                return;
            }
            Hinweis = _kontext.LastHinweis;
            if (Hinweis.Length == 0) FordereSchliessenAn();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (StammdatenException ex) when (!_beendet && ex.Art is StammdatenFehlerart.Ungueltig or StammdatenFehlerart.NichtGefunden or StammdatenFehlerart.Konflikt)
        {
            Hinweis = ex.Art == StammdatenFehlerart.Konflikt
                ? "Overlay-Bestand geändert. Eingabe bleibt erhalten; bitte neu laden und Änderung prüfen."
                : "Name konnte nicht übernommen werden. Eingabe bleibt erhalten.";
        }
        catch (Exception)
        {
            if (!_beendet) Hinweis = "Speicherstatus unklar. Eingabe bleibt erhalten; bitte Bestand neu laden.";
        }
        finally { if (!_beendet) Aendert = false; }
    }

    [RelayCommand(CanExecute = nameof(KannAuswahlAendern))]
    private async Task Loeschen(CancellationToken cancellationToken)
    {
        if (_beendet || !KannAuswahlAendern || Ausgewaehlt is not { } selected ||
            BestehenderBestand is not { } bestand) return;
        long id = selected.Id;
        bool folder = selected.IstOrdner;
        string version = bestand.Version;
        string prompt;
        if (folder)
        {
            int itemCount = ApiOverlayBaum.OverlayIds(selected).Count;
            int childFolders = Flatten(selected.Kinder).Count(node => node.IstOrdner);
            if (itemCount == 0 && childFolders == 0) prompt = string.Empty;
            else prompt = $"Ordner „{selected.Name}“ samt {childFolders} Unterordnern und {itemCount} Overlays löschen?";
        }
        else prompt = $"Overlay „{selected.Name}“ löschen?";

        if (prompt.Length != 0 && !_bestaetigen(prompt)) return;
        cancellationToken.ThrowIfCancellationRequested();
        if (_beendet || !KannAuswahlAendern || !ReferenceEquals(selected, Ausgewaehlt)) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lebensdauerToken);
        CancellationToken ct = linked.Token;
        Aendert = true;
        try
        {
            if (folder) await _verwaltung.OrdnerLoeschen(id, version, ct);
            else await _verwaltung.OverlayLoeschen(id, version, ct);
            ct.ThrowIfCancellationRequested();
            if (_beendet) return;
            var fresh = await AktualisiereNachSchreibenAsync(ct);
            if (fresh is null)
            {
                Hinweis = "Löschen bestätigt, aktueller Bestand konnte nicht neu geladen werden.";
                return;
            }
            Hinweis = _kontext.LastHinweis;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (StammdatenException ex) when (!_beendet && ex.Art is StammdatenFehlerart.Ungueltig or StammdatenFehlerart.NichtGefunden or StammdatenFehlerart.Konflikt)
        {
            Hinweis = ex.Art == StammdatenFehlerart.Konflikt
                ? "Overlay-Bestand geändert. Bitte neu laden; Löschung nicht automatisch wiederholt."
                : "Auswahl konnte nicht gelöscht werden. Bitte Bestand neu laden.";
        }
        catch (Exception)
        {
            if (!_beendet) Hinweis = "Speicherstatus unklar. Bitte Bestand neu laden und Löschung prüfen.";
        }
        finally { if (!_beendet) Aendert = false; }
    }
}
