using System.ComponentModel;
using System.Windows;
using AndyTours.App.ViewModels;

namespace AndyTours.App.Views;

/// <summary>Fenster für die serverseitige Verwaltung von Kategorien.</summary>
public partial class ApiKategorienFenster : Window, IDisposable
{
    private readonly ApiKategorienViewModel _viewModel;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _geladen;
    private bool _geschlossen;

    public ApiKategorienFenster(ApiKategorienViewModel viewModel)
    {
        InitializeComponent();
        AndyTours.App.Funktionen.Oberflaechenskalierung.Anmelden(this);
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += FensterGeladen;
        Closed += FensterGeschlossen;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.Aendert)
        {
            e.Cancel = true;
            _viewModel.StatusText = "Die Änderung wird noch verarbeitet. Bitte warten, bis der Speichervorgang abgeschlossen ist.";
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
        try { await _viewModel.LadenAsync(_lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception) { _viewModel.StatusText = "Kategorien konnten nicht geladen werden. Bitte Verbindung prüfen."; }
    }

    private void FensterGeschlossen(object? sender, EventArgs e)
    {
        if (_geschlossen) return;
        _viewModel.LadenCommand.Cancel();
        _viewModel.AnlegenCommand.Cancel();
        _viewModel.UmbenennenCommand.Cancel();
        _viewModel.LoeschenCommand.Cancel();
        _viewModel.StandardUmschaltenCommand.Cancel();
        _viewModel.ZusammenfuehrenCommand.Cancel();
        Dispose();
    }
}
