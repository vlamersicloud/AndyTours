using System.Windows;
using AndyTours.App.ViewModels;
using AndyTours.App.Funktionen;

namespace AndyTours.App.Views;

/// <summary>
/// Code-Behind des Fensters „Equipment“.
/// </summary>
/// <remarks>
/// Keine Programmlogik: Felder, Prüfung und Speichern liegen im <see cref="EquipmentBearbeitenViewModel"/>.
/// Hier wird nur das Fenster geschlossen, sobald das ViewModel gespeichert hat. „Abbrechen“ ist als
/// Abbrechen-Schaltfläche (<c>IsCancel</c>) markiert und schließt ohne eigenen Code.
/// </remarks>
public partial class EquipmentBearbeitenFenster : Window
{
    private readonly EquipmentBearbeitenViewModel _viewModel;
    private bool _saveSucceeded;
    /// <summary>
    /// Erzeugt das Fenster und verbindet es mit dem ViewModel.
    /// </summary>
    /// <param name="viewModel">ViewModel mit den Feldern des Equipments.</param>
    public EquipmentBearbeitenFenster(EquipmentBearbeitenViewModel viewModel)
    {
        InitializeComponent();
        Oberflaechenskalierung.Anmelden(this);
        DataContext = viewModel;
        viewModel.Gespeichert += (_, _) => { _saveSucceeded = true; DialogResult = true; };
        _viewModel = viewModel;
        Closing += OnClosing;

        // Tastaturbedienung: Fokus in den Namen
        Loaded += (_, _) => NameFeld.Focus();
    }
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_viewModel.Speichert && !_saveSucceeded)
        {
            e.Cancel = true;
        }
    }
}
