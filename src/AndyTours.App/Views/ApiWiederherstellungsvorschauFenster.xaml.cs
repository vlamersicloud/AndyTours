namespace AndyTours.App.Views;

using System.Windows;
using Microsoft.Win32;
using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;

/// <summary>Owned modal for read-only restore metadata preview; closing never applies a restore.</summary>
public partial class ApiWiederherstellungsvorschauFenster : Window, IDisposable
{
    private readonly ApiWiederherstellungsvorschauViewModel _viewModel;
    private bool _disposed;
    private bool _loaded;

    public ApiWiederherstellungsvorschauFenster(ApiWiederherstellungsvorschauViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        AndyTours.App.Funktionen.Oberflaechenskalierung.Anmelden(this);
        DataContext = _viewModel;
        Loaded += LoadedWindow;
        Closed += ClosedWindow;
    }

    private async void LoadedWindow(object sender, RoutedEventArgs e)
    {
        if (_disposed || _loaded) return;
        _loaded = true;
        try { await _viewModel.LadenAsync(); }
        catch (Exception) { /* ViewModel owns safe status reporting. */ }
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (_disposed || _viewModel.Auswahl is not { } selected || !_viewModel.KannDownloaden) return;
        var selectedId = selected.Id;
        var selectedHash = selected.InhaltHash;
        var extension = selected.Art == Sicherungsart.LegacySqlite ? ".db" : ".zip";
        var dialog = new SaveFileDialog
        {
            FileName = $"AndyTours-Sicherung-{selected.Id:N}{extension}",
            DefaultExt = extension,
            AddExtension = true,
            OverwritePrompt = false,
            CheckPathExists = true,
            ValidateNames = true,
            Filter = selected.Art == Sicherungsart.LegacySqlite
                ? "SQLite-Sicherung (*.db)|*.db"
                : "Vollarchiv (*.zip)|*.zip"
        };
        if (dialog.ShowDialog(this) != true || _disposed) return;
        if (_viewModel.Auswahl is not { } current || current.Id != selectedId ||
            !string.Equals(current.InhaltHash, selectedHash, StringComparison.Ordinal)) return;
        try { await _viewModel.DownloadNachAsync(dialog.FileName); }
        catch (Exception) { /* ViewModel reports safe download status and cleans owned temporary files. */ }
    }

    private void ClosedWindow(object? sender, EventArgs e) => Cleanup();

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    private void Cleanup()
    {
        if (_disposed) return;
        _disposed = true;
        Loaded -= LoadedWindow;
        Closed -= ClosedWindow;
        _viewModel.Dispose();
    }
}
