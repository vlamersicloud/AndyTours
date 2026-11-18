namespace AndyTours.App.ViewModels;

public partial class ApiUebersichtViewModel
{
    private readonly ApiTourenauswahl _manuelleAuswahl = new();
    private int _auswahlAnzahl;

    public int AuswahlAnzahl => _auswahlAnzahl;
    public bool HatTourenauswahl => !Laedt && !InlineGesperrt && !EditorOffen && _auswahlAnzahl > 0;
    public IReadOnlyList<AndyTours.Contracts.V1.TourAntwort> TourenAuswahlSnapshot => _manuelleAuswahl.Snapshot;

    public void SetzeTourenauswahl(int anzahl)
    {
        var normalized = Math.Max(0, anzahl);
        if (_auswahlAnzahl == normalized) return;
        _auswahlAnzahl = normalized;
        OnPropertyChanged(nameof(AuswahlAnzahl));
        OnPropertyChanged(nameof(HatTourenauswahl));
        OnPropertyChanged(nameof(TourenAuswahlSnapshot));
        BenachrichtigeTourCommands();
    }

    public void SetzeManuelleTourenauswahl(IEnumerable<long> sichtbareAusgewaehlteIds)
    {
        if (InlineGesperrt) return;
        _manuelleAuswahl.SetzeSichtbare(Touren, sichtbareAusgewaehlteIds);
        SetzeTourenauswahl(_manuelleAuswahl.Count);
    }

    public bool SetzeManuelleTourenauswahl(long tourId, bool ausgewaehlt)
    {
        if (InlineGesperrt) return false;
        var row = Touren.FirstOrDefault(x => x.Id == tourId);
        if (row is null) return false;
        var changed = _manuelleAuswahl.Setze(row, ausgewaehlt);
        SetzeTourenauswahl(_manuelleAuswahl.Count);
        return changed;
    }

    public void HebeTourenauswahlAuf()
    {
        if (InlineGesperrt) return;
        _manuelleAuswahl.Clear();
        SetzeTourenauswahl(0);
    }

    public IReadOnlyList<AndyTours.Contracts.V1.TourAntwort> ErfasseTourenauswahl() => _manuelleAuswahl.Snapshot;

    public void EntferneBestaetigteTouren(IEnumerable<long> ids)
    {
        _manuelleAuswahl.EntferneBestaetigte(ids);
        SetzeTourenauswahl(_manuelleAuswahl.Count);
    }

    partial void OnEditorOffenChanged(bool value)
    {
        OnPropertyChanged(nameof(HatTourenauswahl));
        BenachrichtigeTourCommands();
    }
}
