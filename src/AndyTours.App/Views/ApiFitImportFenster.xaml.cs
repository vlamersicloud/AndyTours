using System.ComponentModel;
using System.IO;
using System.Windows;
using AndyTours.App.ViewModels;
using Microsoft.Win32;

namespace AndyTours.App.Views;

public partial class ApiFitImportFenster : Window, IDisposable
{
    private readonly ApiFitImportViewModel _viewModel;
    private bool _cleaned;

    public ApiFitImportFenster(ApiFitImportViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PruefungAngefordert += PruefungAngefordert;
        Loaded += FensterGeladen;
    }

    private async void FensterGeladen(object sender, RoutedEventArgs e)
    {
        Loaded -= FensterGeladen;
        try { await _viewModel.LadenCommand.ExecuteAsync(null); }
        catch (OperationCanceledException) { }
        catch
        {
            if (!_cleaned && IsLoaded) _viewModel.Status = "Vorhandene Uploads konnten nicht geladen werden.";
        }
    }

    private void DateienAuswaehlen(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "FIT-Dateien auswählen",
            Filter = "FIT-Dateien (*.fit)|*.fit|Alle Dateien (*.*)|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _viewModel.Hinzufuegen(dialog.FileNames);
            _viewModel.Status = "Dateien zur Warteschlange hinzugefügt.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _viewModel.Status = "Dateiauswahl konnte nicht übernommen werden; die übrigen Dateien bleiben erhalten.";
        }
    }

    private void PruefungAngefordert(GarminImportViewModel editor)
    {
        if (_cleaned || !IsLoaded) return;
        var window = new GarminImportFenster(editor) { Owner = this, Title = "FIT-Dateien prüfen" };
        window.ShowDialog();
    }

    private void FensterClosing(object? sender, CancelEventArgs e)
    {
        if (_viewModel.Aendert || _viewModel.EditorOffen)
        {
            e.Cancel = true;
            return;
        }
        Cleanup();
    }

    private void FensterClosed(object? sender, EventArgs e) => Cleanup();
    public void Dispose() { Cleanup(); GC.SuppressFinalize(this); }

    private void Cleanup()
    {
        if (_cleaned) return;
        _cleaned = true;
        Loaded -= FensterGeladen;
        _viewModel.PruefungAngefordert -= PruefungAngefordert;
        _viewModel.Dispose();
    }
}
