using System;
using System.Threading.Tasks;
using System.Windows;

namespace AndyTours.App.Views;

public partial class ApiHauptfenster
{
    internal CancellationToken LebenszeitToken => _lifetime.Token;
    public event Func<Task>? RoutingKonfigurationAngefordert;

    private async void RoutingKonfiguration_Click(object sender, RoutedEventArgs e)
    {
        if (_geschlossen || _verwaltungOffen || _viewModel.Laedt || _viewModel.EditorOffen) return;
        _verwaltungOffen = true;
        try
        {
            if (RoutingKonfigurationAngefordert is { } handler)
                await handler();
            else if (!_geschlossen && IsVisible)
                _viewModel.StatusText = "Routing-Einstellungen sind nicht verfügbar.";
        }
        catch
        {
            if (!_geschlossen && IsVisible)
                _viewModel.StatusText = "Routing-Einstellungen konnten nicht geöffnet werden.";
        }
        finally { _verwaltungOffen = false; }
    }
}
