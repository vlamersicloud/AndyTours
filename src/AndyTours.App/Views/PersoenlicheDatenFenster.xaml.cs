using System.Windows;
using System.ComponentModel;
using AndyTours.App.ViewModels;
using AndyTours.App.Funktionen;

namespace AndyTours.App.Views;

/// <summary>
/// Code-Behind des Fensters „Persönliche Daten“.
/// </summary>
/// <remarks>
/// Keine Programmlogik: Felder, Prüfung und Speichern liegen im <see cref="PersoenlicheDatenViewModel"/>.
/// Hier wird nur das Fenster geschlossen, sobald das ViewModel gespeichert hat. „Abbrechen“ ist als
/// Abbrechen-Schaltfläche (<c>IsCancel</c>) markiert und schließt ohne eigenen Code.
/// </remarks>
public partial class PersoenlicheDatenFenster : Window
{
    private readonly PersoenlicheDatenViewModel _viewModel;
    private bool _saveSucceeded;
    /// <summary>
    /// Erzeugt das Fenster und verbindet es mit dem ViewModel.
    /// </summary>
    /// <param name="viewModel">ViewModel mit den persönlichen Daten.</param>
    public PersoenlicheDatenFenster(PersoenlicheDatenViewModel viewModel)
    {
        InitializeComponent();
        Oberflaechenskalierung.Anmelden(this);
        DataContext = viewModel;
        _viewModel = viewModel;
        viewModel.Gespeichert += (_, _) => { _saveSucceeded = true; DialogResult = true; };
        Closing += OnClosing;

        // Tastaturbedienung: Fokus ins erste Feld
        Loaded += (_, _) => VornameFeld.Focus();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_viewModel.Speichert && !_saveSucceeded) e.Cancel = true;
    }
}
