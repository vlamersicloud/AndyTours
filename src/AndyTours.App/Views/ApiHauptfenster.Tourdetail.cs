using System.Windows;
using AndyTours.Contracts.V1;

namespace AndyTours.App.Views;

public partial class ApiHauptfenster
{
    public event Func<IReadOnlyList<long>, CancellationToken, Task>? TourdetailAngefordert;

    private async void TourdetailOeffnen_Click(object sender, RoutedEventArgs e)
    {
        if (_geschlossen || _verwaltungOffen || _viewModel.Laedt || _viewModel.EditorOffen)
            return;

        var ids = TourenDataGrid.SelectedItems.OfType<TourAntwort>()
            .Select(tour => tour.Id)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            _viewModel.StatusText = "Bitte wählen Sie mindestens eine Tour aus.";
            return;
        }

        var token = _lifetime.Token;
        _verwaltungOffen = true;
        _viewModel.Laedt = true;
        try
        {
            if (TourdetailAngefordert is { } handler)
                await handler(ids, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        {
            if (!_geschlossen && IsVisible)
                _viewModel.StatusText = "Die Tourdetails konnten nicht geöffnet werden.";
        }
        finally
        {
            if (!_geschlossen)
            {
                _viewModel.Laedt = false;
                _verwaltungOffen = false;
            }
        }
    }
}