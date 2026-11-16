using System.ComponentModel;
using System.Windows.Controls;
using AndyTours.Contracts.V1;

namespace AndyTours.App.Views;

public partial class ApiHauptfenster
{
    private async void TourenDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (_geschlossen || _verwaltungOffen || _viewModel.Laedt || _viewModel.EditorOffen || _lifetime.IsCancellationRequested)
            return;

        var sortierung = e.Column.SortMemberPath switch
        {
            "Werte.Datum" => TourenSortierschluessel.Datum,
            "Werte.Startzeit" => TourenSortierschluessel.Startzeit,
            "Werte.Name" => TourenSortierschluessel.Name,
            "Werte.DistanzKm" => TourenSortierschluessel.DistanzKm,
            "Werte.Hoehenmeter" => TourenSortierschluessel.Hoehenmeter,
            "Werte.FahrzeitSekunden" => TourenSortierschluessel.Fahrzeit,
            _ => (TourenSortierschluessel?)null
        };
        if (sortierung is not { } key) return;
        var absteigend = _viewModel.TourenSortierung == key
            ? !_viewModel.TourenAbsteigend
            : false;

        try
        {
            var erfolgreich = await _viewModel.SortiereTourenAsync(key, absteigend, _lifetime.Token);
            if (!erfolgreich || _geschlossen || _lifetime.IsCancellationRequested) return;
            foreach (var column in TourenDataGrid.Columns) column.SortDirection = null;
            var confirmed = TourenDataGrid.Columns.FirstOrDefault(column => column.SortMemberPath == e.Column.SortMemberPath);
            if (confirmed is not null)
                confirmed.SortDirection = _viewModel.TourenAbsteigend ? ListSortDirection.Descending : ListSortDirection.Ascending;
        }
        catch
        {
            if (!_geschlossen && !_lifetime.IsCancellationRequested)
                _viewModel.StatusText = "Tourensortierung konnte nicht angewendet werden.";
        }
    }
}
