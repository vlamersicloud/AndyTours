using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using AndyTours.Application.Stammdaten;
using AndyTours.Application.Tabellenlayout;
using AndyTours.Contracts.V1;

namespace AndyTours.App.Views;

public partial class ApiHauptfenster
{
    private readonly IEinheitentabellenlayout? _spaltenlayout;
    private readonly DispatcherTimer _spaltenTimer = new() { Interval = TimeSpan.FromMilliseconds(650) };
    private CancellationTokenSource? _spaltenLoad;
    private bool _spaltenAnwenden;
    private bool _spaltenBereit;
    private bool _spaltenSpeichert;
    private bool _spaltenErneut;
    private string? _spaltenVersion;
    private ContextMenu? _spaltenMenu;
    private MenuItem? _spaltenReload;
    private readonly Dictionary<Einheitentabellenschluessel, MenuItem> _spaltenMenuItems = new();

    private void SpaltenlayoutDispose()
    {
        if (_spaltenDisposed) return;
        _spaltenDisposed = true;
        ++_spaltenGeneration;
        _spaltenTimer.Stop();
        _spaltenTimer.Tick -= SpaltenlayoutTimerTick;
        _spaltenLoad?.Cancel();
        BeobachterEntfernen();
        Loaded -= SpaltenlayoutLoaded;
        Closed -= SpaltenlayoutClosed;
        if (_spaltenMenu is not null)
        {
            TourenDataGrid.ContextMenu = null;
            _spaltenMenu.Items.Clear();
            _spaltenMenu = null;
            _spaltenReload = null;
            _spaltenMenuItems.Clear();
        }
    }

    private long _spaltenGeneration;
    private bool _spaltenDisposed;
    private bool _spaltenGesperrt;
    private readonly Dictionary<DataGridColumn, (DependencyPropertyDescriptor Width, DependencyPropertyDescriptor Index, DependencyPropertyDescriptor Visibility)> _spaltenBeobachter = new();

    private async void SpaltenlayoutLoaded(object sender, RoutedEventArgs e)
    {
        if (_spaltenDisposed) return;
        InstalliereSpaltenmenue();
        try { await SpaltenlayoutInitialisieren(); }
        catch (Exception) { if (!_geschlossen && !_spaltenDisposed) _viewModel.StatusText = "Spaltenlayout konnte nicht geladen werden."; }
    }

    private async Task SpaltenlayoutInitialisieren()
    {
        if (_spaltenDisposed || _spaltenSpeichert || _spaltenLoad is not null || _spaltenlayout is null) return;
        InstalliereSpaltenmenue();
        _spaltenGesperrt = true;
        _spaltenBereit = false;
        _spaltenVersion = null;
        _spaltenErneut = false;
        _spaltenTimer.Stop();
        BeobachterEntfernen();
        _spaltenBereit = false;
        _spaltenVersion = null;
        _spaltenTimer.Stop();
        var generation = ++_spaltenGeneration;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _spaltenLoad = cts;
        try
        {
            var layout = await _spaltenlayout!.Laden(cts.Token);
            if (cts.IsCancellationRequested || _geschlossen || _spaltenDisposed || generation != _spaltenGeneration) return;
            _spaltenVersion = layout.Version;
            _spaltenAnwenden = true;
            try
            {
                var map = SpaltenZuordnungen();
                foreach (var entry in layout.Anordnung)
                {
                    var column = map[entry.Schluessel];
                    column.DisplayIndex = layout.Anordnung.ToList().FindIndex(x => x.Schluessel == entry.Schluessel);
                    column.Width = entry.Breite is double width ? new DataGridLength(width) : DataGridLength.Auto;
                }
                foreach (var key in Enum.GetValues<Einheitentabellenschluessel>()) map[key].Visibility = layout.Ausgeblendet.Contains(key) ? Visibility.Collapsed : Visibility.Visible;
            }
            finally { _spaltenAnwenden = false; }
            _spaltenBereit = true;
            _spaltenGesperrt = false;
            InstalliereSpaltenmenue();
            BeobachterInstallieren();
            if (_spaltenReload is not null) _spaltenReload.IsEnabled = true;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
        catch (Exception) { if (!cts.IsCancellationRequested && !_geschlossen && !_spaltenDisposed && generation == _spaltenGeneration) _viewModel.StatusText = "Spaltenlayout konnte nicht geladen werden."; }
        finally { if (ReferenceEquals(_spaltenLoad, cts)) _spaltenLoad = null; cts.Dispose(); }
    }

    private void InstalliereSpaltenmenue()
    {
        if (_spaltenMenu is not null) return;
        var menu = new ContextMenu { StaysOpen = true };
        _spaltenReload = new MenuItem { Header = "Spaltenlayout neu laden", IsEnabled = true };
        _spaltenReload.Click += async (_, _) =>
        {
            try { await SpaltenlayoutInitialisieren(); }
            catch (Exception) { if (!_geschlossen && !_spaltenDisposed) _viewModel.StatusText = "Spaltenlayout konnte nicht neu geladen werden."; }
        };
        menu.Items.Add(_spaltenReload);
        menu.Items.Add(new Separator());
        foreach (var pair in SpaltenZuordnungen())
        {
            var key = pair.Key;
            var item = new MenuItem { Header = pair.Value.Header?.ToString() ?? key.ToString(), IsCheckable = true, IsChecked = pair.Value.Visibility == Visibility.Visible, StaysOpenOnClick = true };
            item.Checked += (_, _) => SetzeSichtbarkeit(key, true);
            item.Unchecked += (_, _) => SetzeSichtbarkeit(key, false);
            menu.Items.Add(item);
            _spaltenMenuItems[key] = item;
        }
        menu.Opened += (_, _) => MenuChecksAktualisieren();
        _spaltenMenu = menu;
        TourenDataGrid.ContextMenu = menu;
    }

    private void MenuChecksAktualisieren()
    {
        var vorher = _spaltenAnwenden;
        _spaltenAnwenden = true;
        try
        {
            foreach (var pair in _spaltenMenuItems)
                pair.Value.IsChecked = SpaltenZuordnungen()[pair.Key].Visibility == Visibility.Visible;
        }
        finally { _spaltenAnwenden = vorher; }
    }

    private void SetzeSichtbarkeit(Einheitentabellenschluessel key, bool visible)
    {
        if (!_spaltenBereit || _spaltenAnwenden || _spaltenGesperrt) return;
        var map = SpaltenZuordnungen();
        int shown = map.Values.Count(c => c.Visibility == Visibility.Visible);
        if (!visible && map[key].Visibility == Visibility.Visible && shown <= 1)
        {
            MenuChecksAktualisieren();
            _viewModel.StatusText = "Mindestens eine Tabellenspalte muss sichtbar bleiben.";
            return;
        }
        _spaltenAnwenden = true;
        try { map[key].Visibility = visible ? Visibility.Visible : Visibility.Collapsed; }
        finally { _spaltenAnwenden = false; }
        QueueSpaltenlayout();
    }

    private void SpaltenlayoutAenderung(object? sender, EventArgs e) => QueueSpaltenlayout();
    private void QueueSpaltenlayout()
    {
        if (!_spaltenBereit || _spaltenGesperrt || _spaltenAnwenden || _geschlossen || _spaltenDisposed) return;
        if (_spaltenSpeichert) { _spaltenErneut = true; return; }
        _spaltenTimer.Stop(); _spaltenTimer.Start();
    }
    private async void SpaltenlayoutTimerTick(object? sender, EventArgs e)
    {
        _spaltenTimer.Stop();
        if (_spaltenSpeichert || !_spaltenBereit || _spaltenGesperrt || _spaltenlayout is null || _spaltenVersion is null || _geschlossen || _spaltenDisposed) return;
        _spaltenSpeichert = true;
        _spaltenErneut = false;
        var versionAtStart = _spaltenVersion;
        try
        {
            var snapshot = ErfasseSpaltenlayout(versionAtStart);
            var result = await _spaltenlayout.Speichern(snapshot, _lifetime.Token);
            if (_geschlossen || _spaltenDisposed || _lifetime.IsCancellationRequested) return;
            _spaltenVersion = result.Version;
            if (!_geschlossen && !_spaltenDisposed) _viewModel.StatusText = "Spaltenlayout gespeichert.";
        }
        catch (StammdatenException ex) when (ex.Art == StammdatenFehlerart.Konflikt)
        {
            SperreSpaltenlayout("Spaltenlayout wurde anderswo geändert. Bitte über „Spaltenlayout neu laden“ aktualisieren.");
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested || _spaltenDisposed) { }
        catch (Exception) { SperreSpaltenlayout("Speicherstatus des Spaltenlayouts unklar. Bitte über „Spaltenlayout neu laden“ aktualisieren."); }
        finally
        {
            _spaltenSpeichert = false;
            if (_spaltenErneut && !_spaltenGesperrt && !_geschlossen && !_spaltenDisposed) { _spaltenErneut = false; _spaltenTimer.Start(); }
        }
    }

    private void SperreSpaltenlayout(string message)
    {
        _spaltenGesperrt = true;
        _spaltenBereit = false;
        _spaltenVersion = null;
        _spaltenErneut = false;
        _spaltenTimer.Stop();
        if (!_geschlossen && !_spaltenDisposed) _viewModel.StatusText = message;
    }

    private EinheitentabellenlayoutAnfrage ErfasseSpaltenlayout(string version)
    {
        var map = SpaltenZuordnungen();
        var ordered = map.OrderBy(x => x.Value.DisplayIndex).Select(x => new Einheitentabellenspalte(x.Key,
            x.Value.Width.UnitType == DataGridLengthUnitType.Pixel ? x.Value.Width.Value : null)).ToArray();
        var hidden = map.Where(x => x.Value.Visibility != Visibility.Visible).Select(x => x.Key).ToArray();
        return new EinheitentabellenlayoutAnfrage(version, Array.AsReadOnly(ordered), Array.AsReadOnly(hidden));
    }

    private Dictionary<Einheitentabellenschluessel, DataGridColumn> SpaltenZuordnungen() => new()
    {
        [Einheitentabellenschluessel.Datum] = Spalte("Anzeige.Datum"), [Einheitentabellenschluessel.Startzeit] = Spalte("Anzeige.Startzeit"),
        [Einheitentabellenschluessel.Name] = Spalte("Anzeige.Name"), [Einheitentabellenschluessel.Aktivitaet] = Spalte("Anzeige.Aktivitaet"),
        [Einheitentabellenschluessel.Kategorien] = Spalte("Anzeige.Kategorien"), [Einheitentabellenschluessel.Equipment] = Spalte("Anzeige.Equipment"),
        [Einheitentabellenschluessel.Distanz] = Spalte("Anzeige.DistanzKm"), [Einheitentabellenschluessel.Hoehenmeter] = Spalte("Anzeige.Hoehenmeter"),
        [Einheitentabellenschluessel.Fahrzeit] = Spalte("Anzeige.FahrzeitSekunden"), [Einheitentabellenschluessel.DurchschnittKmh] = Spalte("Anzeige.DurchschnittKmh"),
        [Einheitentabellenschluessel.MaxKmh] = Spalte("Anzeige.MaxKmh"), [Einheitentabellenschluessel.PulsDurchschnitt] = Spalte("Anzeige.HfDurchschnitt"),
        [Einheitentabellenschluessel.PulsMax] = Spalte("Anzeige.HfMax"), [Einheitentabellenschluessel.Trittfrequenz] = Spalte("Anzeige.Trittfrequenz"),
        [Einheitentabellenschluessel.Temperatur] = Spalte("Anzeige.Temperatur"), [Einheitentabellenschluessel.MaxHoehe] = Spalte("Anzeige.MaxHoehe"),
        [Einheitentabellenschluessel.Trainingsbereich] = Spalte("Anzeige.Trainingsbereich"), [Einheitentabellenschluessel.Kommentar] = Spalte("Anzeige.Kommentar")
    };
    private DataGridColumn Spalte(string path) => TourenDataGrid.Columns.Single(c => c.SortMemberPath == path);

    private void BeobachterInstallieren()
    {
        foreach (var column in SpaltenZuordnungen().Values)
        {
            if (_spaltenBeobachter.ContainsKey(column)) continue;
            var w = DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
            var i = DependencyPropertyDescriptor.FromProperty(DataGridColumn.DisplayIndexProperty, typeof(DataGridColumn));
            var v = DependencyPropertyDescriptor.FromProperty(DataGridColumn.VisibilityProperty, typeof(DataGridColumn));
            if (w is null || i is null || v is null) continue;
            w.AddValueChanged(column, SpaltenlayoutAenderung); i.AddValueChanged(column, SpaltenlayoutAenderung); v.AddValueChanged(column, SpaltenlayoutAenderung);
            _spaltenBeobachter.Add(column, (w, i, v));
        }
    }

    private void BeobachterEntfernen()
    {
        foreach (var entry in _spaltenBeobachter)
        {
            entry.Value.Width.RemoveValueChanged(entry.Key, SpaltenlayoutAenderung);
            entry.Value.Index.RemoveValueChanged(entry.Key, SpaltenlayoutAenderung);
            entry.Value.Visibility.RemoveValueChanged(entry.Key, SpaltenlayoutAenderung);
        }
        _spaltenBeobachter.Clear();
    }

    private void SpaltenlayoutClosed(object? sender, EventArgs e) => SpaltenlayoutDispose();
}
