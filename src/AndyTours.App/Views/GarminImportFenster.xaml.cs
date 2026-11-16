using System.ComponentModel;
using System.Windows;
using AndyTours.App.ViewModels;
using AndyTours.App.Funktionen;

namespace AndyTours.App.Views;

public partial class GarminImportFenster : Window
{
    private readonly GarminImportViewModel _viewModel;
    private bool _saved;
    private bool _closed;

    public GarminImportFenster(GarminImportViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Oberflaechenskalierung.Anmelden(this);
        DataContext = viewModel;
        SpalteGeraet.Visibility = viewModel.MehrereGeraete ? Visibility.Visible : Visibility.Collapsed;
        viewModel.Bestaetigt += Bestaetigt;
    }

    private void Bestaetigt(object? sender, EventArgs e)
    {
        _saved = true;
        DialogResult = true;
    }

    private void FensterClosing(object? sender, CancelEventArgs e)
    {
        if (_viewModel.Speichert && !_saved) e.Cancel = true;
    }

    private void FensterClosed(object? sender, EventArgs e)
    {
        if (_closed) return;
        _closed = true;
        _viewModel.Bestaetigt -= Bestaetigt;
        _viewModel.Dispose();
    }
}
