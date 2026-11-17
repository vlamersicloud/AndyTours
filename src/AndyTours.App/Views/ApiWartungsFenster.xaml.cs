using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AndyTours.App.ViewModels;

namespace AndyTours.App.Views;

/// <summary>Code-Behind der API-Wartungsverwaltung; verwaltet modale Editoren und Ansichtslebensdauer.</summary>
public partial class ApiWartungsFenster : Window, IDisposable
{
    private readonly ApiWartungsViewModel _viewModel;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;

    public ApiWartungsFenster(ApiWartungsViewModel viewModel)
    {
        InitializeComponent();
        AndyTours.App.Funktionen.Oberflaechenskalierung.Anmelden(this);
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.WartungEditorAngefordert += WartungEditorAngefordert;
        _viewModel.AlarmEditorAngefordert += AlarmEditorAngefordert;
        Loaded += LoadedWindow;
        Closing += ClosingWindow;
        Closed += ClosedWindow;
    }

    private async void LoadedWindow(object sender, RoutedEventArgs e)
    {
        try { await _viewModel.LadenAsync(_lifetime.Token); }
        catch (OperationCanceledException) { }
    }

    private void WartungEditorAngefordert(object? sender, WartungBearbeitenViewModel vm)
    {
        var window = new WartungBearbeitenFenster(vm) { Owner = this };
        window.ShowDialog();
    }

    private void AlarmEditorAngefordert(object? sender, AlarmBearbeitenViewModel vm)
    {
        var window = new AlarmBearbeitenFenster(vm) { Owner = this };
        window.ShowDialog();
    }

    private void Wartungen_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _viewModel.WartungIds = WartungenGrid.SelectedItems.Cast<AndyTours.Contracts.V1.WartungszeileAntwort>().Select(x => x.Id).ToArray();

    private void Alarme_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _viewModel.AlarmIds = AlarmeGrid.SelectedItems.Cast<AndyTours.Contracts.V1.AlarmzeileAntwort>().Select(x => x.Id).ToArray();

    private void Wartungen_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (ItemsControl.ContainerFromElement(WartungenGrid, e.OriginalSource as DependencyObject) is DataGridRow row && row.Item is AndyTours.Contracts.V1.WartungszeileAntwort item)
        {
            WartungenGrid.SelectedItems.Clear(); row.IsSelected = true;
            _viewModel.WartungIds = [item.Id];
            if (_viewModel.WartungBearbeitenCommand.CanExecute(null)) _viewModel.WartungBearbeitenCommand.Execute(null);
        }
    }

    private void Alarme_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (ItemsControl.ContainerFromElement(AlarmeGrid, e.OriginalSource as DependencyObject) is DataGridRow row && row.Item is AndyTours.Contracts.V1.AlarmzeileAntwort item)
        {
            AlarmeGrid.SelectedItems.Clear(); row.IsSelected = true;
            _viewModel.AlarmIds = [item.Id];
            if (_viewModel.AlarmBearbeitenCommand.CanExecute(null)) _viewModel.AlarmBearbeitenCommand.Execute(null);
        }
    }

    private void Wartungen_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2 && _viewModel.WartungBearbeitenCommand.CanExecute(null)) { _viewModel.WartungBearbeitenCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Delete && _viewModel.WartungenLoeschenCommand.CanExecute(null)) { _viewModel.WartungenLoeschenCommand.Execute(null); e.Handled = true; }
    }

    private void Alarme_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2 && _viewModel.AlarmBearbeitenCommand.CanExecute(null)) { _viewModel.AlarmBearbeitenCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Delete && _viewModel.AlarmeLoeschenCommand.CanExecute(null)) { _viewModel.AlarmeLoeschenCommand.Execute(null); e.Handled = true; }
    }

    private void ClosingWindow(object? sender, CancelEventArgs e)
    {
        if (_viewModel.Aendert || _viewModel.EditorOffen)
        {
            e.Cancel = true;
            return;
        }
        CancelAndDispose();
    }

    private void ClosedWindow(object? sender, EventArgs e)
    {
        CancelAndDispose();
    }

    public void Dispose()
    {
        CancelAndDispose();
        GC.SuppressFinalize(this);
    }

    private void CancelCommands()
    {
        _viewModel.LadenCommand.Cancel();
        _viewModel.NeueWartungCommand.Cancel();
        _viewModel.WartungBearbeitenCommand.Cancel();
        _viewModel.NeuerAlarmCommand.Cancel();
        _viewModel.AlarmBearbeitenCommand.Cancel();
    }

    private void CancelAndDispose()
    {
        if (_disposed) return;
        _disposed = true;
        _viewModel.AbbrechenLesenderVorgaenge();
        _viewModel.WartungEditorAngefordert -= WartungEditorAngefordert;
        _viewModel.AlarmEditorAngefordert -= AlarmEditorAngefordert;
        CancelCommands();
        _viewModel.Dispose();
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
