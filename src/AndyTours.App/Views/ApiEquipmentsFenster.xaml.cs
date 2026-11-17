using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Globalization;
using System.Windows.Data;
using AndyTours.Contracts.V1;
using AndyTours.Client;
using AndyTours.App.ViewModels;

namespace AndyTours.App.Views;

/// <summary>Administrationsfenster für Equipment.</summary>
public partial class ApiEquipmentsFenster : Window, IDisposable
{
    private readonly ApiEquipmentsViewModel _viewModel;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _loaded;
    private bool _closed;
    public ApiEquipmentsFenster(ApiEquipmentsViewModel viewModel)
    {
        InitializeComponent();
        AndyTours.App.Funktionen.Oberflaechenskalierung.Anmelden(this);
        _viewModel = viewModel; DataContext = viewModel;
        Loaded += OnLoaded; Closed += OnClosed;
        _viewModel.EditorAngefordert += OnEditorAngefordert;
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.Aendert || _viewModel.EditorOffen) { e.Cancel = true; return; }
        base.OnClosing(e);
    }
    public void Dispose()
    {
        if (_closed) return; _closed = true; _lifetime.Cancel(); _lifetime.Dispose(); GC.SuppressFinalize(this);
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded || _closed) return; _loaded = true;
        try { await _viewModel.LadenAsync(_lifetime.Token); } catch (OperationCanceledException) { }
    }
    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.EditorAngefordert -= OnEditorAngefordert;
        _viewModel.LadenCommand.Cancel(); _viewModel.NeuCommand.Cancel(); _viewModel.BearbeitenCommand.Cancel(); _viewModel.LoeschenCommand.Cancel(); Dispose();
    }
    private void OnEditorAngefordert(object? sender, EquipmentEditorEventArgs args)
    {
        var editor = new EquipmentBearbeitenFenster(args.ViewModel) { Owner = this };
        editor.ShowDialog();
    }
    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || sender is not DataGrid grid || e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(grid, source) is not DataGridRow { Item: Contracts.V1.EquipmentAntwort item }) return;
        _viewModel.Auswahl = item; if (_viewModel.BearbeitenCommand.CanExecute(null)) _viewModel.BearbeitenCommand.Execute(null); e.Handled = true;
    }
    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid grid || grid.SelectedItem is not Contracts.V1.EquipmentAntwort item || e.Key is not (Key.F2 or Key.Enter or Key.Delete)) return;
        _viewModel.Auswahl = item;
        var command = e.Key == Key.Delete ? _viewModel.LoeschenCommand : _viewModel.BearbeitenCommand;
        if (command.CanExecute(null)) command.Execute(null); e.Handled = true;
    }
}

public sealed class EquipmentAktivitaetConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[1] is not IReadOnlyDictionary<long, string> names) return Binding.DoNothing;
        if (values[0] is not long id) return "(keine)";
        return names.TryGetValue(id, out var name) ? name : $"Nicht vorhanden (#{id})";
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        [Binding.DoNothing, Binding.DoNothing];
}
