using System.ComponentModel;
using System.Windows;
using AndyTours.App.ViewModels;

namespace AndyTours.App.Views;

public partial class ApiRoutingKonfigurationFenster : Window, IDisposable
{
    private readonly ApiRoutingKonfigurationViewModel _viewModel;
    private bool _disposed;
    private bool _clearingPassword;

    public ApiRoutingKonfigurationFenster(ApiRoutingKonfigurationViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.Bestaetigung = Confirm;
        InitializeComponent();
        DataContext = viewModel;
        Loaded += LoadedWindow;
        Closing += ClosingWindow;
        Closed += ClosedWindow;
        _viewModel.EntwurfLeeren += ClearPassword;
    }

    private async void LoadedWindow(object sender, RoutedEventArgs e)
    {
        try { await _viewModel.LadenCommand.ExecuteAsync(default); }
        catch (OperationCanceledException) { }
    }

    private bool Confirm(string message) =>
        MessageBox.Show(this, message, "Routingkonfiguration", MessageBoxButton.YesNo,
            MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    private void OrsSchluesselFeld_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_disposed && !_clearingPassword)
            _viewModel.OrsSchluesselEntwurf = OrsSchluesselFeld.Password;
    }

    private void ClearPassword(object? sender, EventArgs e)
    {
        _clearingPassword = true;
        try { OrsSchluesselFeld.Clear(); }
        finally { _clearingPassword = false; }
    }

    private void ClosingWindow(object? sender, CancelEventArgs e)
    {
        if (_viewModel.Aendert)
        {
            e.Cancel = true;
            MessageBox.Show(this, "Ein Speichervorgang läuft. Bitte warten oder über „Abbrechen“ abbrechen.",
                "Routingkonfiguration", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_viewModel.HatEntwuerfe || _viewModel.HatUnklarenSpeicherstatus)
        {
            string text = _viewModel.HatUnklarenSpeicherstatus
                ? "Speicherstatus ist unklar. Entwürfe beim Schließen verwerfen?"
                : "Ungespeicherte Entwürfe beim Schließen verwerfen?";
            if (!Confirm(text)) { e.Cancel = true; return; }
        }
        Cleanup();
    }

    private void ClosedWindow(object? sender, EventArgs e) => Cleanup();
    public void Dispose() { Cleanup(); GC.SuppressFinalize(this); }

    private void Cleanup()
    {
        if (_disposed) return;
        _disposed = true;
        Loaded -= LoadedWindow;
        Closing -= ClosingWindow;
        Closed -= ClosedWindow;
        _viewModel.EntwurfLeeren -= ClearPassword;
        _clearingPassword = true;
        try { OrsSchluesselFeld.Clear(); }
        finally { _clearingPassword = false; }
        _viewModel.OrsSchluesselEntwurf = string.Empty;
        _viewModel.AdresseEntwurf = string.Empty;
        _viewModel.Bestaetigung = null;
        _viewModel.Dispose();
    }
}
