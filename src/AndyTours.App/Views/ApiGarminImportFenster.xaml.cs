using System.ComponentModel;
using System.Windows;
using AndyTours.App.ViewModels;

namespace AndyTours.App.Views;

public partial class ApiGarminImportFenster : Window, IDisposable
{
    private readonly ApiGarminImportViewModel _viewModel;
    private readonly CancellationTokenSource _pollLifetime = new();
    private bool _disposed;

    public ApiGarminImportFenster(ApiGarminImportViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = viewModel;
        Loaded += LoadedWindow;
        Closing += ClosingWindow;
        Closed += ClosedWindow;
    }

    private async void LoadedWindow(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.LadenAktuellenAsync(_pollLifetime.Token);
            if (_disposed || !IsLoaded) return;
            if (_viewModel.Auftrag is not null)
            await PollWhileOpen(_pollLifetime.Token);
        }
        catch (OperationCanceledException) when (_pollLifetime.IsCancellationRequested) { }
        catch { /* The view model converts expected service errors to safe status text. */ }
    }

    private async Task PollWhileOpen(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(ct))
        {
            if (_disposed || !IsLoaded) return;
            await _viewModel.PollAsync(ct);
        }
    }

    private void ClosingWindow(object? sender, CancelEventArgs e)
    {
        if (_viewModel.Aendert)
        {
            e.Cancel = true;
            return;
        }
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
        _pollLifetime.Cancel();
        _viewModel.BeginClose();
        _viewModel.Dispose();
        _pollLifetime.Dispose();
    }
}
