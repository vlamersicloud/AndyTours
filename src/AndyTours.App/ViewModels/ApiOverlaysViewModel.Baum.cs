using AndyTours.App.Funktionen;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiOverlaysViewModel
{
    private bool _wirdAngezeigt;

    partial void UebernimmAnzeige()
    {
        var state = _kontext.Anzeige;
        if (state is null) return;
        Hinweis = _kontext.LastHinweis;
        var bestand = state.Bestand;
        if (!ApiOverlayBaum.Strukturgleich(Wurzeln, bestand))
        {
            var expanded = Wurzeln.SelectMany(Flatten).Where(n => n.IstOrdner).ToDictionary(n => n.Id, n => n.IstAufgeklappt);
            long? selectedFolder = Ausgewaehlt?.IstOrdner == true ? Ausgewaehlt.Id : null;
            long? selectedOverlay = Ausgewaehlt?.IstOverlay == true ? Ausgewaehlt.Id : null;
            IReadOnlyList<ApiOverlayknoten> roots;
            try
            {
                roots = ApiOverlayBaum.Erstellen(bestand, (node, value) => Umschaltwunsch(node, value), expanded, selectedFolder, selectedOverlay);
            }
            catch (InvalidOperationException)
            {
                _baumGueltig = false;
                Hinweis = "Der Overlay-Bestand ist ungültig. Bitte neu laden.";
                OnPropertyChanged(nameof(IstBereit)); OnPropertyChanged(nameof(KannAendern));
                return;
            }
            foreach (var old in Wurzeln.SelectMany(Flatten)) old.Ausgewaehlt -= KnotenAusgewaehlt;
            Wurzeln.Clear();
            foreach (var root in roots)
            {
                foreach (var node in Flatten(root)) node.Ausgewaehlt += KnotenAusgewaehlt;
                Wurzeln.Add(root);
            }
            _baumGueltig = true;
            Ausgewaehlt = Flatten(Wurzeln).FirstOrDefault(n => n.IstAusgewaehlt);
            PopulateColorOptions(state);
        }
        else
        {
            ApiOverlayBaum.AktualisiereZustaende(Wurzeln, bestand);
            PopulateColorOptions(state);
        }
        OnPropertyChanged(nameof(IstBereit)); OnPropertyChanged(nameof(KannAendern)); OnPropertyChanged(nameof(IstLeer));
    }

    partial void AuswahlAnzeigen(ApiOverlayknoten? value)
    {
        if (_wirdAngezeigt) return;
        _wirdAngezeigt = true;
        try
        {
            Farbauswahl.Clear();
            if (value?.Overlay is { } meta && BestehenderBestand is { } bestand)
            {
                var common = ApiOverlayFarboptionen.Gemeinsam(bestand.GemeinsameFarbe);
                Farbauswahl.Add(common);
                foreach (var option in ApiOverlayFarboptionen.Alle()) Farbauswahl.Add(option);
                GewaehlteFarbe = meta.Farbe is { } own
                    ? Farbauswahl.First(o => o.Farbe == own)
                    : common;
            }
            else GewaehlteFarbe = null;
        }
        finally { _wirdAngezeigt = false; }
    }

    partial void Umschaltwunsch(ApiOverlayknoten node, bool value);

    private void PopulateColorOptions(ApiOverlayAnzeige state)
    {
        _wirdAngezeigt = true;
        try
        {
            GemeinsameFarbauswahl.Clear();
            foreach (var color in ApiOverlayFarboptionen.Alle()) GemeinsameFarbauswahl.Add(color);
            GewaehlteGemeinsameFarbe = GemeinsameFarbauswahl.FirstOrDefault(o => o.Farbe == state.Bestand.GemeinsameFarbe);
            if (Ausgewaehlt?.Overlay is { } metadata)
            {
                Farbauswahl.Clear();
                var common = ApiOverlayFarboptionen.Gemeinsam(state.Bestand.GemeinsameFarbe);
                Farbauswahl.Add(common);
                foreach (var color in ApiOverlayFarboptionen.Alle()) Farbauswahl.Add(color);
                GewaehlteFarbe = metadata.Farbe is { } own ? Farbauswahl.FirstOrDefault(o => o.Farbe == own) : common;
            }
        }
        finally { _wirdAngezeigt = false; }
    }

    private void KnotenAusgewaehlt(object? sender, EventArgs e)
    {
        if (sender is not ApiOverlayknoten selected) return;
        foreach (var node in Wurzeln.SelectMany(Flatten)) node.IstAusgewaehlt = ReferenceEquals(node, selected);
        Ausgewaehlt = selected;
    }

    private static IEnumerable<ApiOverlayknoten> Flatten(IEnumerable<ApiOverlayknoten> nodes)
    {
        foreach (var node in nodes)
            foreach (var descendant in Flatten(node)) yield return descendant;
    }

    private static IEnumerable<ApiOverlayknoten> Flatten(ApiOverlayknoten node)
    {
        yield return node;
        foreach (var child in node.Kinder)
            foreach (var descendant in Flatten(child)) yield return descendant;
    }
}
