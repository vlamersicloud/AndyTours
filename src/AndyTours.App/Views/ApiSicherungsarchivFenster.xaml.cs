using System.ComponentModel;
using System.Windows;
using AndyTours.App.ViewModels;

namespace AndyTours.App.Views;

public partial class ApiSicherungsarchivFenster : Window, IDisposable
{
    private readonly ApiSicherungsarchivViewModel _viewModel;
    private bool _disposed;

    public ApiSicherungsarchivFenster(ApiSicherungsarchivViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        Closing += ClosingWindow;
        Closed += ClosedWindow;
    }

    private void ClosingWindow(object? sender, CancelEventArgs e)
    {
        if (!_disposed && _viewModel.IstBeschaeftigt)
        {
            e.Cancel = true;
        }
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
        Closing -= ClosingWindow;
        Closed -= ClosedWindow;
        _viewModel.Dispose();
    }
}
