using System.Windows;

namespace AndyTours.App.Views;

public partial class ApiHauptfenster
{
    public event Func<Task>? GarminGeraetImportierenAngefordert;

    private async void GarminGeraetImportieren_Click(object sender, RoutedEventArgs e)
    {
        if (_geschlossen || _verwaltungOffen || _viewModel.Laedt || _viewModel.EditorOffen) return;

        _verwaltungOffen = true;
        try
        {
            if (GarminGeraetImportierenAngefordert is { } handler)
                await handler();
        }
        catch (Exception)
        {
            _viewModel.StatusText = "Geräteimport konnte nicht geöffnet werden.";
        }
        finally
        {
            _verwaltungOffen = false;
        }

        if (_geschlossen || !IsVisible) return;
        try
        {
            await _viewModel.AktualisierenAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            _viewModel.StatusText = "Übersicht konnte nach dem Geräteimport nicht aktualisiert werden.";
        }
    }
}
