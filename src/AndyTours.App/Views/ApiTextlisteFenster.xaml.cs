using System.ComponentModel;
using System.Windows;
using AndyTours.App.ViewModels;

namespace AndyTours.App.Views;

/// <summary>Lebenszyklusfenster für eine einzelne unterstützte API-Textliste.</summary>
public partial class ApiTextlisteFenster : Window, IDisposable
{
    private readonly ApiTextlisteViewModel _viewModel;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;

    public ApiTextlisteFenster(ApiTextlisteViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Title = viewModel.Art switch
        {
            AndyTours.Contracts.V1.Textlistenart.Trainingsbereich => "Trainingsbereiche",
            AndyTours.Contracts.V1.Textlistenart.Taetigkeit => "Tätigkeiten",
            _ => "Textliste"
        };
        Loaded += LoadedWindow;
        Closing += ClosingWindow;
        Closed += ClosedWindow;
    }

    private async void LoadedWindow(object sender, RoutedEventArgs e)
    {
        try { await _viewModel.LadenAsync(_lifetime.Token); }
        catch (OperationCanceledException) { }
    }

    private void ClosingWindow(object? sender, CancelEventArgs e)
    {
        if (_viewModel.Aendert) { e.Cancel = true; return; }
        Cleanup();
    }

    private void ClosedWindow(object? sender, EventArgs e) => Cleanup();
    public void Dispose() { Cleanup(); GC.SuppressFinalize(this); }

    private void Cleanup()
    {
        if (_disposed) return;
        _disposed = true;
        _viewModel.AbbrechenLesen();
        _viewModel.LadenCommand.Cancel();
        _viewModel.Dispose();
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
