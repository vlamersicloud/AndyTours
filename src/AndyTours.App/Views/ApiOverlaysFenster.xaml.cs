using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using AndyTours.App.ViewModels;
using AndyTours.App.Funktionen;
using AndyTours.Core.Modell;

namespace AndyTours.App.Views;

public partial class ApiOverlaysFenster : Window
{
    private readonly ApiOverlaysViewModel _vm;
    private bool _closingApproved;

    public ApiOverlaysFenster(ApiOverlaysViewModel vm)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        InitializeComponent();
        Oberflaechenskalierung.Anmelden(this);
        DataContext = vm;
        vm.VerschiebezielWaehlen = WaehleVerschiebeziel;
        vm.GpxDateienAngefordert += GpxDateienAngefordert;
        vm.NameBearbeiten += NameBearbeiten;
        vm.Schliessen += Schliessen;
    }

    private async void FensterGeladen(object sender, RoutedEventArgs e)
    {
        if (_vm.LadenCommand.IsRunning) return;
        try { await _vm.LadenCommand.ExecuteAsync(null); }
        catch (Exception) { if (IsLoaded) _vm.Hinweis = "Overlay-Bestand konnte nicht geladen werden. Bitte Verbindung prüfen."; }
    }

    private Auswahleintrag? WaehleVerschiebeziel(string name, IReadOnlyList<Auswahleintrag> choices, Auswahleintrag? current)
    {
        var vm = new EintragWaehlenViewModel("Verschieben nach", $"Neuer Ordner für „{name}“:", choices, current);
        var dialog = new EintragWaehlenFenster(vm) { Owner = this };
        return dialog.ShowDialog() == true ? vm.Ergebnis : null;
    }

    private void GpxDateienAngefordert(object? sender, EventArgs e)
    {
        var picker = new OpenFileDialog { Filter = "GPX-Dateien (*.gpx)|*.gpx|Alle Dateien (*.*)|*.*", Multiselect = true, CheckFileExists = true };
        if (picker.ShowDialog(this) == true) _vm.GpxDateienHinzufuegen(picker.FileNames);
    }

    private void NameBearbeiten(object? sender, EventArgs e)
    {
        NamensFeld.Focus();
        NamensFeld.SelectAll();
    }

    private void Schliessen(object? sender, EventArgs e) => Close();

    private void FensterSchliesst(object? sender, CancelEventArgs e)
    {
        if (_closingApproved) return;
        if (_vm.IstBeschaeftigt)
        {
            MessageBox.Show(this, "Bitte warten, bis die laufende Änderung abgeschlossen ist.", "Overlays", MessageBoxButton.OK, MessageBoxImage.Information);
            e.Cancel = true;
            return;
        }
        var warnings = new List<string>();
        if (_vm.HatUngeklaerteOrdneranlage || _vm.HatUnklareGpxImporte)
            warnings.Add("Es gibt Anfragen mit unklarem Serverstatus.");
        if (_vm.Importdateien.Any(row => !row.Fertig && !row.Unklar))
            warnings.Add("Nicht importierte Dateiauswahlen werden beim Schließen verworfen.");
        if (warnings.Count != 0 && MessageBox.Show(this, string.Join(Environment.NewLine, warnings) + Environment.NewLine + "Trotzdem schließen?", "Overlays", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        _closingApproved = true;
    }

    private void FensterGeschlossen(object? sender, EventArgs e)
    {
        _vm.GpxDateienAngefordert -= GpxDateienAngefordert;
        _vm.NameBearbeiten -= NameBearbeiten;
        _vm.Schliessen -= Schliessen;
        _vm.VerschiebezielWaehlen = null;
        _vm.Dispose();
    }

    private void BaumDoppelklick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        if (Tabellenbedienung.SucheVorfahr<CheckBox>(e.OriginalSource) is null &&
            Tabellenbedienung.SucheVorfahr<TreeViewItem>(e.OriginalSource)?.DataContext is ApiOverlayknoten { IstOverlay: true } node)
        {
            _vm.ZeigeGanzCommand.Execute(node);
            e.Handled = true;
        }
    }

    private void BaumRechtsklick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        var item = Tabellenbedienung.SucheVorfahr<TreeViewItem>(source);
        if (item?.DataContext is ApiOverlayknoten node) { item.IsSelected = true; item.Focus(); }
    }
}
