using System.ComponentModel;
using System.Windows;
using AndyTours.App.ViewModels;
using AndyTours.App.Funktionen;

namespace AndyTours.App.Views;

public partial class ApiTourenMehrfachFenster : Window, IDisposable
{
    private readonly ApiTourenMehrfachViewModel _viewModel;
    private bool _cleanupRequested;
    private bool _cleaned;

    public ApiTourenMehrfachFenster(ApiTourenMehrfachViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        Oberflaechenskalierung.Anmelden(this);
        DataContext = _viewModel;
        Loaded += FensterGeladen;
        Closing += FensterWirdGeschlossen;
        Closed += FensterGeschlossen;
    }

    private void FensterGeladen(object sender, RoutedEventArgs e) =>
        SchliessenButton.Focus();

    private void FensterWirdGeschlossen(object? sender, CancelEventArgs e)
    {
        if (_cleanupRequested) return;
        if (_viewModel.IstBeschaeftigt)
        {
            e.Cancel = true;
            MessageBox.Show(this, "Der Vorgang läuft noch. Bitte warten oder über Abbrechen anhalten.",
                _viewModel.Titel, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_viewModel.IstUnklar &&
            MessageBox.Show(this,
                "Der Ausgang ist ungewiss. Beim Schließen wird die Möglichkeit zur identischen Wiederholung verworfen. Trotzdem schließen?",
                _viewModel.Titel, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
    }

    private void FensterGeschlossen(object? sender, EventArgs e) => Cleanup();

    public void Dispose()
    {
        if (_cleaned) return;
        _cleanupRequested = true;
        Cleanup();
        Close();
        GC.SuppressFinalize(this);
    }

    private void Cleanup()
    {
        if (_cleaned) return;
        _cleaned = true;
        Loaded -= FensterGeladen;
        Closing -= FensterWirdGeschlossen;
        Closed -= FensterGeschlossen;
        DataContext = null;
        _viewModel.Dispose();
    }
}
