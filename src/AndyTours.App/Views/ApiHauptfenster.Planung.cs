using AndyTours.App.Funktionen;

namespace AndyTours.App.Views;

public partial class ApiHauptfenster
{
    public event Func<CancellationToken, Task>? TourenplanungAngefordert;

    private async void TourPlanen_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_geschlossen || _verwaltungOffen || _viewModel.Laedt || _viewModel.EditorOffen) return;
        _verwaltungOffen = true;
        _viewModel.Laedt = true;
        try
        {
            if (TourenplanungAngefordert is { } handlers)
                foreach (Func<CancellationToken, Task> handler in handlers.GetInvocationList()) await handler(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch { if (!_geschlossen) _viewModel.StatusText = "Die Tourenplanung konnte nicht geöffnet werden."; }
        finally { _viewModel.Laedt = false; _verwaltungOffen = false; }
    }
}
