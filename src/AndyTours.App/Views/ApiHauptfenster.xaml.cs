using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AndyTours.App.ViewModels;

namespace AndyTours.App.Views;

public partial class ApiHauptfenster : Window, IDisposable
{
    private readonly ApiUebersichtViewModel _viewModel;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _geladen;
    private bool _geschlossen;

    public ApiHauptfenster(ApiUebersichtViewModel viewModel, Uri backendAdresse)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        BackendAdresseText.Text = backendAdresse.GetLeftPart(UriPartial.Authority);
        Loaded += FensterGeladen;
        Closed += FensterGeschlossen;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.EditorOffen)
        {
            e.Cancel = true;
            _viewModel.StatusText = "Bitte zuerst den Bearbeitungsdialog schließen.";
            return;
        }
        base.OnClosing(e);
    }

    public void Dispose()
    {
        if (_geschlossen) return;
        _geschlossen = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    private async void FensterGeladen(object sender, RoutedEventArgs e)
    {
        if (_geladen || _geschlossen) return;
        _geladen = true;
        try { await _viewModel.AktualisierenAsync(_lifetime.Token); }
        catch (OperationCanceledException) { }
    }

    private void TourenDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || e.OriginalSource is not DependencyObject ursprung) return;
        if (ItemsControl.ContainerFromElement(grid, ursprung) is not DataGridRow { Item: Contracts.V1.TourAntwort } row)
            return;

        if (_viewModel.TourBearbeitenCommand.CanExecute(null))
            _viewModel.TourBearbeitenCommand.Execute(null);
        e.Handled = true;
    }

    private void FensterGeschlossen(object? sender, EventArgs e)
    {
        if (_geschlossen) return;
        _geschlossen = true;
        _lifetime.Cancel();
        _viewModel.AktualisierenCommand.Cancel();
        _viewModel.NaechsteSeiteCommand.Cancel();
        _viewModel.VorherigeSeiteCommand.Cancel();
        _viewModel.NeueTourCommand.Cancel();
        _viewModel.TourBearbeitenCommand.Cancel();
        _viewModel.TourKopierenCommand.Cancel();
        _viewModel.TourLoeschenCommand.Cancel();
        _lifetime.Dispose();
    }
}

public sealed class DauerConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long sekunden) return "–";
        var stunden = sekunden / 3600;
        var minuten = sekunden % 3600 / 60;
        return $"{stunden.ToString("N0", CultureInfo.GetCultureInfo("de-DE"))}:{minuten:00} h";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
