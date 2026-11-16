using CommunityToolkit.Mvvm.Input;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiOverlaysViewModel
{
    private OverlayOrdnerAnlegenAnfrage? _offeneOrdneranlage;
    private long? _neuerOrdnerId;
    public bool HatUngeklaerteOrdneranlage => _offeneOrdneranlage is not null;

    [RelayCommand(CanExecute = nameof(KannAendern))]
    private async Task OrdnerAnlegen(CancellationToken cancellationToken)
    {
        if (_beendet || !KannAendern || IstBeschaeftigt || BestehenderBestand is not { } bestand) return;
        if (_offeneOrdneranlage is null)
        {
            long? parent = Ausgewaehlt switch
            {
                { IstOrdner: true } selectedFolder => selectedFolder.Id,
                { IstOverlay: true, Overlay.OrdnerId: { } folderId } => folderId,
                _ => null
            };
            string name = EindeutigerOrdnername(bestand, parent);
            _offeneOrdneranlage = new OverlayOrdnerAnlegenAnfrage(bestand.Version, Guid.NewGuid(), parent, name);
            OnPropertyChanged(nameof(HatUngeklaerteOrdneranlage));
        }

        var request = _offeneOrdneranlage;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lebensdauerToken);
        CancellationToken ct = linked.Token;
        Aendert = true;
        try
        {
            var created = await _verwaltung.OrdnerAnlegen(request, ct);
            ct.ThrowIfCancellationRequested();
            if (_beendet) return;
            if (created is null || created.Id <= 0) throw new InvalidOperationException();
            var fresh = await AktualisiereNachSchreibenAsync(ct);
            _offeneOrdneranlage = null;
            _neuerOrdnerId = fresh is not null && fresh.Ordner.Any(folder => folder.Id == created.Id) ? created.Id : null;
            OnPropertyChanged(nameof(HatUngeklaerteOrdneranlage));
            if (_neuerOrdnerId is { } id)
            {
                MarkiereKnoten(id, false);
                FordereNamensbearbeitungAn();
            }
            if (fresh is null) Hinweis = "Ordneranlage bestätigt, aktueller Bestand konnte aber nicht neu geladen werden.";
            else if (_neuerOrdnerId is null) Hinweis = "Anfrage bestätigt, der Ordner ist im aktuellen Bestand nicht mehr vorhanden.";
            else Hinweis = _kontext.LastHinweis;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (!_beendet) Hinweis = "Ordneranlage möglicherweise erfolgt. Erneut klicken wiederholt nur dieselbe Anfrage; zuerst Bestand prüfen.";
        }
        catch (StammdatenException ex) when (!_beendet && ex.Art is StammdatenFehlerart.Ungueltig or StammdatenFehlerart.NichtGefunden or StammdatenFehlerart.Konflikt)
        {
            _offeneOrdneranlage = null;
            OnPropertyChanged(nameof(HatUngeklaerteOrdneranlage));
            Hinweis = ex.Art == StammdatenFehlerart.Konflikt
                ? "Overlay-Bestand geändert. Bitte neu laden und Ordneranlage bewusst erneut starten."
                : "Ordner konnte nicht angelegt werden. Bitte Eingabe und Ziel prüfen.";
        }
        catch (Exception)
        {
            if (!_beendet) Hinweis = "Ordneranlage möglicherweise erfolgt. Erneut klicken wiederholt nur dieselbe Anfrage; zuerst Bestand prüfen.";
        }
        finally { if (!_beendet) Aendert = false; }
    }

    private async Task<OverlayBestandAntwort?> AktualisiereNachSchreibenAsync(CancellationToken ct)
    {
        var before = _kontext.Anzeige;
        await _kontext.LadenAsync(ct);
        ct.ThrowIfCancellationRequested();
        var after = _kontext.Anzeige;
        if (ReferenceEquals(before, after) || after is null) return null;
        return after?.Bestand;
    }

    private void MarkiereKnoten(long id, bool overlay)
    {
        var node = Wurzeln.SelectMany(Flatten).FirstOrDefault(item => item.Id == id && item.IstOverlay == overlay);
        if (node is null) { _neuerOrdnerId = null; return; }
        foreach (var item in Wurzeln.SelectMany(Flatten)) item.IstAusgewaehlt = ReferenceEquals(item, node);
        var folders = BestehenderBestand?.Ordner.ToDictionary(folder => folder.Id) ?? new Dictionary<long, OverlayOrdnerAntwort>();
        var visited = new HashSet<long>();
        long? parentId = node.Ordner?.ElternId ?? node.Overlay?.OrdnerId;
        while (parentId is { } currentId && visited.Add(currentId) && folders.TryGetValue(currentId, out var parent))
        {
            var parentNode = Wurzeln.SelectMany(Flatten).FirstOrDefault(item => item.IstOrdner && item.Id == currentId);
            if (parentNode is not null) parentNode.IstAufgeklappt = true;
            parentId = parent.ElternId;
        }
        node.IstAusgewaehlt = true;
        Ausgewaehlt = node;
    }

    private static string EindeutigerOrdnername(OverlayBestandAntwort bestand, long? parent)
    {
        var names = bestand.Ordner.Where(folder => folder.ElternId == parent)
            .Select(folder => folder.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        const string stem = "Neuer Ordner";
        if (!names.Contains(stem)) return stem;
        for (int suffix = 2; ; suffix++)
        {
            string candidate = $"{stem} {suffix}";
            if (!names.Contains(candidate)) return candidate;
        }
    }

}
