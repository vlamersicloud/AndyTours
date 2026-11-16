using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AndyTours.App.ViewModels;

namespace AndyTours.App.Views;

public partial class ApiHauptfenster : Window, IDisposable
{
    private readonly ApiUebersichtViewModel _viewModel;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _geladen;
    private bool _geschlossen;
    private bool _verwaltungOffen;

    public event Func<Task>? AktivitaetenVerwaltenAngefordert;
    public event Func<Task>? KategorienVerwaltenAngefordert;
    public event Func<Task>? EquipmentsVerwaltenAngefordert;
    public event Func<Task>? ProfilBearbeitenAngefordert;
    public event Func<Task>? WartungVerwaltenAngefordert;
    public event Func<CancellationToken, Task<string?>>? FaelligeAlarmeLadenAngefordert;

    public ApiHauptfenster(ApiUebersichtViewModel viewModel, Uri backendAdresse)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        BackendAdresseText.Text = backendAdresse.GetLeftPart(UriPartial.Authority);
        Loaded += FensterGeladen;
        Closed += FensterGeschlossen;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        var ownedWindowOffen = System.Windows.Application.Current?.Windows
            .OfType<Window>()
            .Any(window => window != this && window.Owner == this && window.IsVisible) == true;
        if (_verwaltungOffen || _viewModel.EditorOffen || ownedWindowOffen)
        {
            e.Cancel = true;
            _viewModel.StatusText = "Bitte zuerst alle geöffneten Dialogfenster schließen.";
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
        try
        {
            await _viewModel.AktualisierenAsync(_lifetime.Token);
            if (_geschlossen || !IsVisible || _lifetime.IsCancellationRequested) return;
            if (FaelligeAlarmeLadenAngefordert is not { } handler) return;
            _viewModel.Laedt = true;
            try
            {
                var warning = await handler(_lifetime.Token);
                if (!_lifetime.IsCancellationRequested && !_geschlossen && IsVisible && !string.IsNullOrWhiteSpace(warning))
                    MessageBox.Show(this, warning, "Wartungen fällig", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch (Exception) { /* Startup-Warnung darf den Start nicht verhindern. */ }
            finally { _viewModel.Laedt = false; }
        }
        catch (OperationCanceledException) { }
    }

    private async void AktivitaetenVerwalten_Click(object sender, RoutedEventArgs e)
    {
        if (_geschlossen || _verwaltungOffen || _viewModel.Laedt || _viewModel.EditorOffen) return;
        _verwaltungOffen = true;
        try
        {
            if (AktivitaetenVerwaltenAngefordert is { } handler)
                await handler();
        }
        catch (Exception)
        {
            _viewModel.StatusText = "Die Aktivitätenverwaltung konnte nicht geöffnet werden.";
        }
        finally
        {
            _verwaltungOffen = false;
        }
    }

    private async void KategorienVerwalten_Click(object sender, RoutedEventArgs e)
    {
        if (_geschlossen || _verwaltungOffen || _viewModel.Laedt || _viewModel.EditorOffen) return;
        _verwaltungOffen = true;
        try
        {
            if (KategorienVerwaltenAngefordert is { } handler)
                await handler();
        }
        catch (Exception)
        {
            _viewModel.StatusText = "Die Kategorienverwaltung konnte nicht geöffnet werden.";
        }
        finally
        {
            _verwaltungOffen = false;
        }
    }


    private async void EquipmentsVerwalten_Click(object sender, RoutedEventArgs e)
    {
        if (_geschlossen || _verwaltungOffen || _viewModel.Laedt || _viewModel.EditorOffen) return;
        _verwaltungOffen = true;
        _viewModel.Laedt = true;
        try { if (EquipmentsVerwaltenAngefordert is { } handler) await handler(); }
        catch (Exception) { _viewModel.StatusText = "Die Equipmentverwaltung konnte nicht geöffnet werden."; }
        finally { _viewModel.Laedt = false; _verwaltungOffen = false; }
        if (_geschlossen || !IsVisible) return;
        try { await _viewModel.AktualisierenAsync(CancellationToken.None); }
        catch (Exception) { _viewModel.StatusText = "Übersicht konnte nach der Verwaltung nicht aktualisiert werden."; }
    }


    private async void ProfilBearbeiten_Click(object sender, RoutedEventArgs e)
    {
        if (_geschlossen || _verwaltungOffen || _viewModel.Laedt || _viewModel.EditorOffen) return;
        _verwaltungOffen = true;
        _viewModel.Laedt = true;
        try { if (ProfilBearbeitenAngefordert is { } handler) await handler(); }
        catch (Exception) { _viewModel.StatusText = "Das Profil konnte nicht geöffnet werden."; }
        finally { _viewModel.Laedt = false; _verwaltungOffen = false; }
        if (_geschlossen || !IsVisible) return;
        try { await _viewModel.AktualisierenAsync(CancellationToken.None); }
        catch (Exception) { _viewModel.StatusText = "Übersicht konnte nach der Profilbearbeitung nicht aktualisiert werden."; }
    }

    private async void WartungenVerwalten_Click(object sender, RoutedEventArgs e)
    {
        if (_geschlossen || _verwaltungOffen || _viewModel.Laedt || _viewModel.EditorOffen) return;
        _verwaltungOffen = true;
        _viewModel.Laedt = true;
        try { if (WartungVerwaltenAngefordert is { } handler) await handler(); }
        catch (Exception) { _viewModel.StatusText = "Wartungsverwaltung konnte nicht geöffnet werden."; }
        finally { _viewModel.Laedt = false; _verwaltungOffen = false; }
        if (_geschlossen || !IsVisible) return;
        try { await _viewModel.AktualisierenAsync(CancellationToken.None); }
        catch (Exception) { _viewModel.StatusText = "Übersicht konnte nach der Wartungsverwaltung nicht aktualisiert werden."; }
    }

    private void TourenDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || sender is not DataGrid grid || e.OriginalSource is not DependencyObject ursprung) return;
        if (ItemsControl.ContainerFromElement(grid, ursprung) is not DataGridRow { Item: Contracts.V1.TourAntwort tour } row)
            return;

        _viewModel.AusgewaehlteTour = tour;
        if (_viewModel.TourBearbeitenCommand.CanExecute(null))
            _viewModel.TourBearbeitenCommand.Execute(null);
        e.Handled = true;
    }

    private void FensterGeschlossen(object? sender, EventArgs e)
    {
        if (_geschlossen) return;
        _geschlossen = true;
        _lifetime.Cancel();
        _viewModel.AktualisierenCommand.Cancel();
        _viewModel.NaechsteSeiteCommand.Cancel();
        _viewModel.VorherigeSeiteCommand.Cancel();
        _viewModel.NeueTourCommand.Cancel();
        _viewModel.TourBearbeitenCommand.Cancel();
        _viewModel.TourKopierenCommand.Cancel();
        _viewModel.TourLoeschenCommand.Cancel();
        _lifetime.Dispose();
    }
}

public sealed class DauerConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long sekunden) return "–";
        var stunden = sekunden / 3600;
        var minuten = sekunden % 3600 / 60;
        return $"{stunden.ToString("N0", CultureInfo.GetCultureInfo("de-DE"))}:{minuten:00} h";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
