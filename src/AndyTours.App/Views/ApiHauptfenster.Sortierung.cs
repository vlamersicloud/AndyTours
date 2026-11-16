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
            "Anzeige.Datum" => TourenSortierschluessel.Datum,
            "Anzeige.Startzeit" => TourenSortierschluessel.Startzeit,
            "Anzeige.Name" => TourenSortierschluessel.Name,
            "Anzeige.Aktivitaet" => TourenSortierschluessel.Aktivitaet,
            "Anzeige.Kategorien" => TourenSortierschluessel.Kategorien,
            "Anzeige.Equipment" => TourenSortierschluessel.Equipment,
            "Anzeige.DistanzKm" => TourenSortierschluessel.DistanzKm,
            "Anzeige.Hoehenmeter" => TourenSortierschluessel.Hoehenmeter,
            "Anzeige.FahrzeitSekunden" => TourenSortierschluessel.Fahrzeit,
            "Anzeige.DurchschnittKmh" => TourenSortierschluessel.DurchschnittKmh,
            "Anzeige.MaxKmh" => TourenSortierschluessel.MaxKmh,
            "Anzeige.HfDurchschnitt" => TourenSortierschluessel.HfDurchschnitt,
            "Anzeige.HfMax" => TourenSortierschluessel.HfMax,
            "Anzeige.Trittfrequenz" => TourenSortierschluessel.Trittfrequenz,
            "Anzeige.Temperatur" => TourenSortierschluessel.Temperatur,
            "Anzeige.MaxHoehe" => TourenSortierschluessel.MaxHoehe,
            "Anzeige.Trainingsbereich" => TourenSortierschluessel.Trainingsbereich,
            "Anzeige.Kommentar" => TourenSortierschluessel.Kommentar,

            // Alte XAML-Pfade bleiben als Routing-Aliase erhalten.
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

        var ct = _lifetime.Token;
        try
        {
            var erfolgreich = await _viewModel.SortiereTourenAsync(key, absteigend, ct);
            if (!erfolgreich || _geschlossen || ct.IsCancellationRequested) return;
            foreach (var column in TourenDataGrid.Columns) column.SortDirection = null;
            var confirmed = TourenDataGrid.Columns.FirstOrDefault(column => column.SortMemberPath == e.Column.SortMemberPath);
            if (confirmed is not null)
                confirmed.SortDirection = _viewModel.TourenAbsteigend ? ListSortDirection.Descending : ListSortDirection.Ascending;
        }
        catch
        {
            if (!_geschlossen && !ct.IsCancellationRequested)
                _viewModel.StatusText = "Tourensortierung konnte nicht angewendet werden.";
        }
    }
}