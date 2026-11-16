using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using AndyTours.App.ViewModels;

namespace AndyTours.App.Views;

public partial class ApiOverlaysFenster : Window
{
    private readonly ApiOverlaysViewModel _vm;
    private bool _closingApproved;

    public ApiOverlaysFenster(ApiOverlaysViewModel vm)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        InitializeComponent();
        DataContext = vm;
        vm.GpxDateienAngefordert += GpxDateienAngefordert;
        vm.NameBearbeiten += NameBearbeiten;
        vm.Schliessen += Schliessen;
    }

    private async void FensterGeladen(object sender, RoutedEventArgs e)
    {
        if (!_vm.LadenCommand.IsRunning) await _vm.LadenCommand.ExecuteAsync(null);
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
        var item = ItemsControl.ContainerFromElement((ItemsControl)sender, source) as TreeViewItem;
        if (item?.DataContext is ApiOverlayknoten { IstOverlay: true } node)
            _vm.ZeigeGanzCommand.Execute(node);
    }

    private void BaumRechtsklick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        var item = ItemsControl.ContainerFromElement((ItemsControl)sender, source) as TreeViewItem;
        if (item?.DataContext is ApiOverlayknoten node) node.IstAusgewaehlt = true;
    }
}
