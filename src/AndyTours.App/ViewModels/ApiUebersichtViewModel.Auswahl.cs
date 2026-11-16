namespace AndyTours.App.ViewModels;

public partial class ApiUebersichtViewModel
{
    private int _auswahlAnzahl;

    public int AuswahlAnzahl => _auswahlAnzahl;
    public bool HatTourenauswahl => !Laedt && !EditorOffen && _auswahlAnzahl > 0;

    public void SetzeTourenauswahl(int anzahl)
    {
        var normalized = Math.Max(0, anzahl);
        if (_auswahlAnzahl == normalized) return;
        _auswahlAnzahl = normalized;
        OnPropertyChanged(nameof(AuswahlAnzahl));
        OnPropertyChanged(nameof(HatTourenauswahl));
        BenachrichtigeTourCommands();
    }

    partial void OnEditorOffenChanged(bool value)
    {
        OnPropertyChanged(nameof(HatTourenauswahl));
        BenachrichtigeTourCommands();
    }
}
