using System.ComponentModel;
using System.Windows;
using AndyTours.App.ViewModels;

namespace AndyTours.App.Views;

public partial class ApiSicherungseinstellungenFenster : Window, IDisposable
{
    private readonly ApiSicherungseinstellungenViewModel _viewModel;
    private bool _disposed;

    public ApiSicherungseinstellungenFenster(ApiSicherungseinstellungenViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += LoadedWindow;
        Closing += ClosingWindow;
        Closed += ClosedWindow;
    }

    private async void LoadedWindow(object sender, RoutedEventArgs e)
    {
        try { await _viewModel.LadenAsync(); }
        catch (OperationCanceledException) { }
    }

    private void ClosingWindow(object? sender, CancelEventArgs e)
    {
        if (_viewModel.Aendert) { e.Cancel = true; return; }
        Cleanup();
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
        Closing -= ClosingWindow;
        Closed -= ClosedWindow;
        _viewModel.BeginClose();
        _viewModel.Dispose();
    }
}
