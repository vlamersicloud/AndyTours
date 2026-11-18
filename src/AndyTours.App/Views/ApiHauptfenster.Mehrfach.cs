using System.Windows;
using System.Windows.Input;
using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;

namespace AndyTours.App.Views;

public partial class ApiHauptfenster
{
    public event Func<IReadOnlyList<TourAntwort>, CancellationToken, Task<ApiTourenMehrfachStatus?>>? TourenKategorienAngefordert;
    public event Func<IReadOnlyList<TourAntwort>, CancellationToken, Task<ApiTourenMehrfachStatus?>>? TourenAktivitaetAngefordert;
    public event Func<IReadOnlyList<TourAntwort>, CancellationToken, Task<ApiTourenMehrfachStatus?>>? TourenEquipmentAngefordert;
    public event Func<IReadOnlyList<TourAntwort>, CancellationToken, Task<ApiTourenMehrfachStatus?>>? TourenLoeschenAngefordert;

    private async void KategorienMehrfach_Click(object sender, RoutedEventArgs e) => await FuehreMehrfachAus(TourenKategorienAngefordert);
    private async void AktivitaetMehrfach_Click(object sender, RoutedEventArgs e) => await FuehreMehrfachAus(TourenAktivitaetAngefordert);
    private async void EquipmentMehrfach_Click(object sender, RoutedEventArgs e) => await FuehreMehrfachAus(TourenEquipmentAngefordert);
    private async void LoeschenMehrfach_Click(object sender, RoutedEventArgs e) => await FuehreMehrfachAus(TourenLoeschenAngefordert);

    private async Task FuehreMehrfachAus(Func<IReadOnlyList<TourAntwort>, CancellationToken, Task<ApiTourenMehrfachStatus?>>? callback)
    {
        if (_geschlossen || _inlineDisposed || !IsVisible || _inlineBusy || _inlineDraft is not null ||
            _viewModel.InlineGesperrt || _verwaltungOffen || _viewModel.Laedt || _viewModel.EditorOffen) return;
        var token = _lifetime.Token;
        if (callback is null) { _viewModel.StatusText = "Die Mehrfachaktion ist noch nicht verfügbar."; return; }
        _verwaltungOffen = true;
        var wasLaedt = false;
        try
        {
            if (!Lebt(token)) return;
            var selected = _viewModel.ErfasseTourenauswahl().Select(Abbild).ToArray();
            if (selected.Length == 0)
            {
                if (Lebt(token)) _viewModel.StatusText = "Bitte zuerst Touren auswählen.";
                return;
            }
            if (selected.Any(t => t.Id <= 0 || !VersionGueltig(t.Version) || t.Werte is null ||
                    t.Werte.KategorieIds is null || t.Werte.KategorieIds.Any(id => id <= 0)) ||
                selected.Select(t => t.Id).Distinct().Count() != selected.Length)
            {
                if (Lebt(token)) _viewModel.StatusText = "Die Auswahl ist ungültig oder enthält doppelte Touren. Bitte Liste neu laden.";
                return;
            }

            wasLaedt = _viewModel.Laedt;
            _viewModel.Laedt = true;
            var outcome = await callback(Array.AsReadOnly(selected), token);
            if (!Lebt(token)) return;
            _viewModel.Laedt = false;
            wasLaedt = false;
            if (outcome == ApiTourenMehrfachStatus.Bestaetigt)
                _viewModel.EntferneBestaetigteTouren(selected.Select(t => t.Id));

            var before = _viewModel.Aktualisierungsstand;
            try { await _viewModel.AktualisierenAsync(token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception) { }
            if (!Lebt(token)) return;
            var fresh = _viewModel.Aktualisierungsstand > before;
            var hint = _viewModel.StatusText;
            _viewModel.StatusText = ApiTourenMehrfachRueckmeldung.Text(outcome ?? ApiTourenMehrfachStatus.Bereit, fresh, hint);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        {
            if (Lebt(token)) _viewModel.StatusText = "Mehrfachaktion konnte nicht bestätigt werden. Bitte Liste laden und Ergebnis prüfen.";
        }
        finally
        {
            if (Lebt(token) && _viewModel.Laedt != wasLaedt) _viewModel.Laedt = wasLaedt;
            _verwaltungOffen = false;
        }
    }

    private bool Lebt(CancellationToken token) => !_geschlossen && !_inlineDisposed && IsVisible && !token.IsCancellationRequested;
    private static TourAntwort Abbild(TourAntwort tour) => tour with { Werte = tour.Werte with { KategorieIds = (long[])tour.Werte.KategorieIds.Clone() } };
    private static bool VersionGueltig(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private async void TourenDataGrid_PreviewKeyDownMehrfach(object sender, KeyEventArgs e)
    {
        // Delete belongs to the active editor; never turn it into a tour action.
        if (_geschlossen || _inlineDisposed || _inlineBusy || _inlineDraft is not null || _viewModel.InlineGesperrt) return;
        if (e.Key != Key.Delete || Keyboard.Modifiers != ModifierKeys.None) return;
        e.Handled = true;
        await FuehreMehrfachAus(TourenLoeschenAngefordert);
    }
}
