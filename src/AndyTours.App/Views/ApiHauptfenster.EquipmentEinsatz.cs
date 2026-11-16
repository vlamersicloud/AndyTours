using System.Windows;

namespace AndyTours.App.Views;

public partial class ApiHauptfenster
{
    public event Func<CancellationToken, Task>? EquipmentEinsatzAngefordert;

    private async void EquipmentEinsatz_Click(object sender, RoutedEventArgs e)
    {
        if (_geschlossen || !IsVisible || _verwaltungOffen || _viewModel.Laedt || _viewModel.EditorOffen || _lifetime.IsCancellationRequested)
            return;

        var ct = _lifetime.Token;
        _verwaltungOffen = true;
        _viewModel.Laedt = true;
        try
        {
            if (EquipmentEinsatzAngefordert is not { } handler)
            {
                if (!_geschlossen && IsVisible && !ct.IsCancellationRequested)
                    _viewModel.StatusText = "Die Equipment-Einsatzanzeige ist nicht verfügbar.";
                return;
            }
            await handler(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception)
        {
            if (!_geschlossen && IsVisible && !ct.IsCancellationRequested)
                _viewModel.StatusText = "Der Equipment-Einsatz konnte nicht geladen werden.";
        }
        finally
        {
            _verwaltungOffen = false;
            if (!_geschlossen) _viewModel.Laedt = false;
        }
    }
}
