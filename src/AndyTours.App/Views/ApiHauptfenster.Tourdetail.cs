using System.Windows;
using AndyTours.Contracts.V1;
using AndyTours.App.ViewModels;

namespace AndyTours.App.Views;

public partial class ApiHauptfenster
{
    public event Func<IReadOnlyList<long>, CancellationToken, Task>? TourdetailAngefordert;

    private async void TourdetailOeffnen_Click(object sender, RoutedEventArgs e)
    {
        if (!KannTourdetailOeffnen()) return;
        var ids = TourenDataGrid.SelectedItems.OfType<TourAntwort>()
            .Select(tour => tour.Id).Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            _viewModel.StatusText = "Bitte wählen Sie mindestens eine Tour aus.";
            return;
        }
        await OeffneTourdetails(ids);
    }

    private async void RekordTourOeffnen_Click(object sender, RoutedEventArgs e)
    {
        if (!KannTourdetailOeffnen()) return;
        var row = (sender as FrameworkElement)?.DataContext as BestleistungszeileAntwort;
        var id = ApiRekordNavigation.TourId(row, _viewModel.Jahresrekorde, _viewModel.Einzelrekorde);
        if (id is null)
        {
            _viewModel.StatusText = "Für diesen Rekord ist keine gültige Tour mehr verfügbar.";
            return;
        }
        await OeffneTourdetails([id.Value]);
    }

    private bool KannTourdetailOeffnen() =>
        !_geschlossen && IsVisible && !_verwaltungOffen && !_viewModel.Laedt && !_viewModel.EditorOffen;

    private async Task OeffneTourdetails(IReadOnlyList<long> ids)
    {
        if (!KannTourdetailOeffnen()) return;
        var token = _lifetime.Token;
        _verwaltungOffen = true;
        _viewModel.Laedt = true;
        try
        {
            token.ThrowIfCancellationRequested();
            if (TourdetailAngefordert is { } handler)
                await handler(Array.AsReadOnly(ids.ToArray()), token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        {
            if (!_geschlossen && IsVisible && !token.IsCancellationRequested)
                _viewModel.StatusText = "Die Tourdetails konnten nicht geöffnet werden.";
        }
        finally
        {
            _verwaltungOffen = false;
            if (!_geschlossen)
                _viewModel.Laedt = false;
        }
    }
}
