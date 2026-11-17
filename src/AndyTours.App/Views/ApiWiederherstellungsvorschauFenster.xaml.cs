namespace AndyTours.App.Views;

using System.Windows;
using AndyTours.App.ViewModels;

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
