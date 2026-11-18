using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Controls;
using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;

namespace AndyTours.App.Views;

public partial class ApiHauptfenster
{
    private bool _auswahlAnwenden;
    private bool _auswahlInitialisiert;
    private bool _auswahlDisposed;

    private void InitialisiereManuelleAuswahl()
    {
        if (_auswahlInitialisiert || _auswahlDisposed) return;
        _auswahlInitialisiert = true;
        _viewModel.PropertyChanged += ManuelleAuswahlPropertyChanged;
        _viewModel.Touren.CollectionChanged += ManuelleAuswahlCollectionChanged;
        StelleSichtbareAuswahlWiederHer();
    }

    private void VerarbeiteManuelleTourenauswahl(object sender, SelectionChangedEventArgs e)
    {
        if (_auswahlDisposed || _geschlossen || _viewModel.InlineGesperrt || _auswahlAnwenden || _viewModel.Laedt || sender is not DataGrid grid) return;
        try
        {
            var ids = grid.SelectedItems.OfType<TourAntwort>().Select(t => t.Id).ToArray();
            _viewModel.SetzeManuelleTourenauswahl(ids);
            _viewModel.AusgewaehlteTour = grid.SelectedItems.OfType<TourAntwort>().FirstOrDefault();
        }
        catch (ArgumentException)
        {
            _viewModel.StatusText = "Die sichtbare Tourenauswahl konnte nicht übernommen werden.";
            StelleSichtbareAuswahlWiederHer();
        }
    }

    private void ManuelleAuswahlPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_auswahlDisposed || _geschlossen) return;
        if (e.PropertyName is nameof(ApiUebersichtViewModel.Laedt) or nameof(ApiUebersichtViewModel.AuswahlAnzahl)
            or nameof(ApiUebersichtViewModel.TourenAuswahlSnapshot))
            StelleSichtbareAuswahlWiederHer();
    }

    private void ManuelleAuswahlCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        StelleSichtbareAuswahlWiederHer();

    private void StelleSichtbareAuswahlWiederHer()
    {
        if (_auswahlDisposed || _geschlossen || _viewModel.InlineGesperrt || !_auswahlInitialisiert || _viewModel.Laedt ||
            !Dispatcher.CheckAccess() || TourenDataGrid is null) return;
        _auswahlAnwenden = true;
        try
        {
            var ids = _viewModel.ErfasseTourenauswahl().Select(t => t.Id).ToHashSet();
            TourenDataGrid.SelectedItems.Clear();
            foreach (var tour in TourenDataGrid.Items.OfType<TourAntwort>())
                if (ids.Contains(tour.Id)) TourenDataGrid.SelectedItems.Add(tour);
        }
        finally { _auswahlAnwenden = false; }
    }

    private void AuswahlAufheben_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_auswahlDisposed || _geschlossen || _viewModel.InlineGesperrt) return;
        _viewModel.HebeTourenauswahlAuf();
        StelleSichtbareAuswahlWiederHer();
    }

    private void DisposeManuelleAuswahl()
    {
        if (_auswahlDisposed) return;
        _auswahlDisposed = true;
        if (_auswahlInitialisiert)
        {
            _viewModel.PropertyChanged -= ManuelleAuswahlPropertyChanged;
            _viewModel.Touren.CollectionChanged -= ManuelleAuswahlCollectionChanged;
        }
    }
}
