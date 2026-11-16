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
        CancellationToken ct = _lifetime.Token;
        try
        {
            if (TourenplanungAngefordert is { } handlers)
                foreach (Func<CancellationToken, Task> handler in handlers.GetInvocationList()) await handler(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch { if (!_geschlossen) _viewModel.StatusText = "Die Tourenplanung konnte nicht geöffnet werden."; }
        finally { if (!_geschlossen) _viewModel.Laedt = false; _verwaltungOffen = false; }
    }
}
