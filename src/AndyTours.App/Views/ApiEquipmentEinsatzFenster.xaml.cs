using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using AndyTours.App.Funktionen;
using AndyTours.App.ViewModels;
using AndyTours.Core.Modell;

namespace AndyTours.App.Views;

/// <summary>Read-only Fenster für den serverseitigen Equipment-Einsatzbericht.</summary>
public partial class ApiEquipmentEinsatzFenster : Window, IDisposable
{
    private readonly ApiEquipmentEinsatzViewModel _viewModel;
    private bool _disposed;
    private bool _closed;

    public ApiEquipmentEinsatzFenster(ApiEquipmentEinsatzViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        Oberflaechenskalierung.Anmelden(this);
        DataContext = _viewModel;
        Closed += OnClosed;
    }

    private void EinsatzDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        Einsatzspalte? column = e.Column.SortMemberPath switch
        {
            nameof(Einsatzzeile.Name) => Einsatzspalte.Equipment,
            nameof(Einsatzzeile.VorherKm) => Einsatzspalte.VorherKm,
            nameof(Einsatzzeile.GesamtKm) => Einsatzspalte.GesamtKm,
            nameof(Einsatzzeile.GesamtHm) => Einsatzspalte.GesamtHm,
            _ => null
        };
        if (column is not { } selected || _disposed || _closed) return;

        bool ascending = e.Column.SortDirection is null ? true : e.Column.SortDirection != ListSortDirection.Ascending;
        try
        {
            _viewModel.Sortiere(selected, ascending);
            foreach (var item in EinsatzDataGrid.Columns) item.SortDirection = null;
            e.Column.SortDirection = ascending ? ListSortDirection.Ascending : ListSortDirection.Descending;
        }
        catch (ArgumentException)
        {
            // Bei ungültigem Sortierschlüssel bleibt die Anzeige unverändert.
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        Closed -= OnClosed;
        DataContext = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (!_closed) Close();
        GC.SuppressFinalize(this);
    }
}
