namespace AndyTours.App.Views;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Input;
using AndyTours.App.Funktionen;
using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;

/// <summary>Coordinates immutable tour inline drafts and CAS saves; never edits a TourAntwort row.</summary>
public partial class ApiHauptfenster
{
    private ApiTourInlineEntwurf? _inlineDraft;
    private CancellationTokenSource? _inlineSave;
    private bool _inlineApply, _inlineBusy;
    private long _inlineUiGeneration;
    private bool _inlineDisposed;
    private bool _inlineDialogStart;
    private int _inlineDialogMouseTimestamp = -1;
    private void InlineBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (_viewModel.InlineNeuLadenNoetig)
        {
            e.Cancel = true;
            if (!_geschlossen) _viewModel.StatusText = "Bitte zuerst Liste aktualisieren; vorherige Änderung nicht automatisch erneut speichern.";
            return;
        }
        if (_geschlossen || _inlineDisposed || _viewModel.Laedt || _viewModel.EditorOffen || _verwaltungOffen || _inlineBusy || _viewModel.AuswahlAnzahl != 1 || e.Row.Item is not TourAntwort tour || _viewModel.AusgewaehlteTour?.Id != tour.Id ||
            _viewModel.ErfasseTourenauswahl().Count != 1 || _viewModel.ErfasseTourenauswahl()[0].Id != tour.Id ||
            e.Column is not DataGridColumn column || InlineField(column.SortMemberPath) is null)
        { e.Cancel = true; return; }
        if (!_viewModel.InlineLookupsBereit)
        {
            e.Cancel = true;
            var row = tour; var columnToResume = column; var generation = ++_inlineUiGeneration;
            _ = LadeLookupsUndStarteEditAsync(row, columnToResume, generation,
                e.EditingEventArgs is TextCompositionEventArgs composition ? composition.Text : null);
            return;
        }
        _inlineDraft = _viewModel.InlineEntwurfFuer(tour);
        _viewModel.SetzeInlineBearbeitung(true);
        _inlineUiGeneration++;
    }

    private async Task LadeLookupsUndStarteEditAsync(TourAntwort tour, DataGridColumn column, long generation, string? initialText)
    {
        try
        {
            var loaded = await _viewModel.LadeInlineLookupsAsync(_lifetime.Token);
            if (!loaded || _geschlossen || generation != _inlineUiGeneration || _viewModel.AuswahlAnzahl != 1 || _viewModel.AusgewaehlteTour?.Id != tour.Id) return;
            await Dispatcher.InvokeAsync(() =>
            {
                if (_geschlossen || generation != _inlineUiGeneration || !ReferenceEquals(TourenDataGrid.SelectedItem, tour)) return;
                TourenDataGrid.CurrentCell = new DataGridCellInfo(tour, column);
                if (TourenDataGrid.BeginEdit() && initialText is not null && column.GetCellContent(tour) is TextBox editor)
                {
                    editor.Text = initialText;
                    editor.CaretIndex = editor.Text.Length;
                }
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (!_geschlossen && generation == _inlineUiGeneration) _viewModel.StatusText = "Inline-Lookups nicht verfügbar; bitte Verbindung prüfen."; }
    }

    private void InlinePreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (_inlineDraft is null || e.Row.Item is not TourAntwort tour || tour.Id != _inlineDraft.TourId || e.Column is not DataGridColumn column) return;
        var field = InlineField(column.SortMemberPath);
        if (field is null) return;
        if (field == "Aktivitaet" || field == "Equipment" || field == "Trainingsbereich")
        {
            if (FindeInlineCombo(e.EditingElement) is ComboBox combo && _viewModel.InlineLookupsBereit)
            {
                combo.ItemsSource = field switch
                {
                    "Aktivitaet" => _viewModel.InlineAktivitaeten.Select(x => new InlineLookup(x.Id, x.Name)).ToArray(),
                    "Equipment" => new[] { new InlineLookup(0, "(kein)") }.Concat(_viewModel.InlineEquipments.Select(x => new InlineLookup(x.Id, x.Name))).ToArray(),
                    _ => _viewModel.InlineTrainingsbereiche.Select(x => new InlineLookup(0, x)).ToArray()
                };
                combo.DisplayMemberPath = nameof(InlineLookup.Name); combo.SelectedValuePath = nameof(InlineLookup.Id);
                var selected = field switch { "Aktivitaet" => _inlineDraft.Werte.AktivitaetId, "Equipment" => _inlineDraft.Werte.EquipmentId ?? 0, _ => 0 };
                combo.SelectedItem = combo.Items.Cast<InlineLookup>().FirstOrDefault(x => field == "Trainingsbereich" ? x.Name == _inlineDraft.Werte.Trainingsbereich : x.Id == selected);
                combo.Focus();
            }
            return;
        }
        if (e.EditingElement is TextBox textBox && e.EditingEventArgs is not TextCompositionEventArgs)
        {
            textBox.Text = field switch
            {
                "Datum" => _inlineDraft.Werte.Datum.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.GetCultureInfo("de-DE")),
                "Startzeit" => _inlineDraft.Werte.Startzeit?.ToString("H:mm", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                "Name" => _inlineDraft.Werte.Name,
                "Distanz" => _inlineDraft.Werte.DistanzKm == 0 ? string.Empty : _inlineDraft.Werte.DistanzKm.ToString(System.Globalization.CultureInfo.GetCultureInfo("de-DE")),
                "Hoehenmeter" => _inlineDraft.Werte.Hoehenmeter == 0 ? string.Empty : _inlineDraft.Werte.Hoehenmeter.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "Fahrzeit" => _inlineDraft.Werte.FahrzeitSekunden == 0 ? string.Empty : $"{_inlineDraft.Werte.FahrzeitSekunden / 3600}:{_inlineDraft.Werte.FahrzeitSekunden % 3600 / 60:00}:{_inlineDraft.Werte.FahrzeitSekunden % 60:00}",
                "MaxKmh" => _inlineDraft.Werte.MaxGeschwindigkeit == 0 ? string.Empty : _inlineDraft.Werte.MaxGeschwindigkeit.ToString(System.Globalization.CultureInfo.GetCultureInfo("de-DE")),
                "PulsDurchschnitt" => _inlineDraft.Werte.HfDurchschnitt == 0 ? string.Empty : _inlineDraft.Werte.HfDurchschnitt.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "PulsMax" => _inlineDraft.Werte.HfMax == 0 ? string.Empty : _inlineDraft.Werte.HfMax.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "Trittfrequenz" => _inlineDraft.Werte.Trittfrequenz == 0 ? string.Empty : _inlineDraft.Werte.Trittfrequenz.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "Temperatur" => _inlineDraft.Werte.Temperatur == 0 ? string.Empty : _inlineDraft.Werte.Temperatur.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "MaxHoehe" => _inlineDraft.Werte.MaxHoehe == 0 ? string.Empty : _inlineDraft.Werte.MaxHoehe.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "Trainingsbereich" => _inlineDraft.Werte.Trainingsbereich,
                _ => string.Empty
            };
            textBox.SelectAll();
        }
    }

    private async void InlineCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        try { await InlineCellEditEndingAsync(e); }
        catch (Exception)
        {
            e.Cancel = true;
            if (!_geschlossen && !_inlineDisposed)
                _viewModel.StatusText = "Inline-Bearbeitung konnte nicht abgeschlossen werden. Bitte schließen und neu laden; nicht automatisch erneut speichern.";
        }
    }

    private async Task InlineCellEditEndingAsync(DataGridCellEditEndingEventArgs e)
    {
        if (_inlineApply) return;
        if (e.EditAction == DataGridEditAction.Cancel)
        {
            e.Cancel = true;
            if (_inlineBusy) return;
            _inlineBusy = true;
            try
            {
                await Dispatcher.InvokeAsync(() => { if (!_geschlossen) BeendeNativeInlineBearbeitung(); }, System.Windows.Threading.DispatcherPriority.Background);
                _inlineUiGeneration++;
                _viewModel.VerwirfInlineEntwurf();
            }
            finally
            {
                _inlineBusy = false;
                if (!_geschlossen) _viewModel.SetzeInlineBearbeitung(_inlineDraft is not null);
            }
            return;
        }
        if (e.EditAction == DataGridEditAction.Commit) e.Cancel = true;
        if (e.EditAction != DataGridEditAction.Commit || _inlineBusy || _inlineDraft is null || e.Row.Item is not TourAntwort row || e.Column is not DataGridColumn col) return;
        e.Cancel = true;
        var field = InlineField(col.SortMemberPath); if (field is null) return;
        var combo = FindeInlineCombo(e.EditingElement);
        var value = e.EditingElement is TextBox box ? box.Text : string.Empty;
        if (field is "Aktivitaet" or "Equipment" or "Trainingsbereich")
        {
            if (combo?.SelectedItem is not InlineLookup item) { MarkiereInlineFehler(e.EditingElement, "Bitte einen gültigen Eintrag auswählen."); return; }
            if (field == "Trainingsbereich")
            {
                if (!_viewModel.InlineTrainingsbereiche.Contains(item.Name)) { MarkiereInlineFehler(e.EditingElement, "Trainingsbereich ist nicht mehr verfügbar."); return; }
            }
            else if (field == "Equipment" && item.Id == 0)
            {
                if (item.Name != "(kein)") { MarkiereInlineFehler(e.EditingElement, "Ungültiges Equipment."); return; }
            }
            else
            {
                var options = field == "Aktivitaet" ? _viewModel.InlineAktivitaeten : _viewModel.InlineEquipments;
                if (!options.Any(x => x.Id == item.Id && x.Name == item.Name) ||
                    options.Count(x => x.Name == item.Name) != 1 ||
                    (field == "Equipment" && item.Name == "(kein)"))
                {
                    combo.BorderBrush = Brushes.Red;
                    combo.ToolTip = "Lookup ist nicht eindeutig. Bitte Stammdaten prüfen.";
                    return;
                }
            }
            value = item.Name;
        }
        var parsed = _inlineDraft.Aendere(field, value, _viewModel.InlineAktivitaeten, _viewModel.InlineEquipments);
        if (!parsed.Valid) { MarkiereInlineFehler(e.EditingElement, parsed.Error); return; }
        if (!parsed.Draft.HatAenderungen)
        {
            _inlineBusy = true;
            try { await Dispatcher.InvokeAsync(() => { if (!_geschlossen) BeendeNativeInlineBearbeitung(); }, System.Windows.Threading.DispatcherPriority.Background); }
            finally { _inlineBusy = false; if (!_geschlossen) _viewModel.SetzeInlineBearbeitung(_inlineDraft is not null); }
            return;
        }
        _inlineBusy = true; var generation = _inlineUiGeneration;
        var saveSource = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _inlineSave = saveSource;
        var token = saveSource.Token; var saved = false;
        try
        {
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            token.ThrowIfCancellationRequested();
            if (_geschlossen || generation != _inlineUiGeneration || _viewModel.InlineFilterAenderungAusstehend) return;
            saved = await _viewModel.SpeichereInlineAsync(parsed.Draft, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (!_geschlossen && generation == _inlineUiGeneration) _viewModel.StatusText = "Speicherstatus unklar. Bitte aktualisieren; nicht automatisch erneut speichern."; }
        finally
        {
            if (!_geschlossen && generation == _inlineUiGeneration)
            {
                BeendeNativeInlineBearbeitung();
                if (saved) _viewModel.StatusText = "Inline-Änderung gespeichert.";
            }
            _inlineBusy = false;
            if (!_geschlossen) _viewModel.SetzeInlineBearbeitung(_inlineDraft is not null);
            if (ReferenceEquals(_inlineSave, saveSource)) _inlineSave = null;
            saveSource.Dispose();
        }
        if (saved && !_geschlossen && _inlineDraft is null && generation == _inlineUiGeneration)
        {
            try { await _viewModel.AktualisierenAsync(_lifetime.Token); }
            catch (OperationCanceledException) { }
            catch (Exception) { if (!_geschlossen) _viewModel.StatusText = "Gespeichert; Aktualisierung fehlgeschlagen. Bitte Liste aktualisieren."; }
        }
    }

    private static void MarkiereInlineFehler(FrameworkElement editor, string error)
    {
        var control = FindeInlineCombo(editor) as Control ?? editor as Control;
        if (control is null) return;
        control.BorderBrush = Brushes.Red;
        control.BorderThickness = new Thickness(2);
        control.ToolTip = error;
    }

    private static ComboBox? FindeInlineCombo(DependencyObject root)
    {
        if (root is ComboBox combo) return combo;
        if (root is FrameworkElement element) element.ApplyTemplate();
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindeInlineCombo(VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        return null;
    }

    private sealed record InlineLookup(long Id, string Name);
    private static string? InlineField(string? path) => path switch
    {
        "Anzeige.Datum"=>"Datum", "Anzeige.Startzeit"=>"Startzeit", "Anzeige.Name"=>"Name", "Anzeige.Aktivitaet"=>"Aktivitaet", "Anzeige.Equipment"=>"Equipment",
        "Anzeige.DistanzKm"=>"Distanz", "Anzeige.Hoehenmeter"=>"Hoehenmeter", "Anzeige.FahrzeitSekunden"=>"Fahrzeit", "Anzeige.MaxKmh"=>"MaxKmh",
        "Anzeige.HfDurchschnitt"=>"PulsDurchschnitt", "Anzeige.HfMax"=>"PulsMax", "Anzeige.Trittfrequenz"=>"Trittfrequenz", "Anzeige.Temperatur"=>"Temperatur", "Anzeige.MaxHoehe"=>"MaxHoehe", "Anzeige.Trainingsbereich"=>"Trainingsbereich", _=>null
    };
    private void BeendeNativeInlineBearbeitung()
    {
        _inlineApply = true;
        var ended = false;
        try
        {
            var cellEnded = TourenDataGrid.CancelEdit(DataGridEditingUnit.Cell);
            var rowEnded = TourenDataGrid.CancelEdit(DataGridEditingUnit.Row);
            ended = cellEnded && rowEnded;
        }
        catch (Exception)
        {
            if (!_geschlossen) _viewModel.StatusText = "Zellenbearbeitung konnte nicht beendet werden. Bitte Fenster schließen; nicht erneut speichern.";
        }
        finally
        {
            _inlineApply = false;
            if (ended) _inlineDraft = null;
            if (!_inlineBusy) _viewModel.SetzeInlineBearbeitung(_inlineDraft is not null);
        }
    }

    private void InlinePreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_inlineBusy) { e.Handled = true; return; }
        var row = Tabellenbedienung.SucheVorfahr<DataGridRow>(e.OriginalSource);
        if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left &&
            row is { Item: TourAntwort tour } && ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(row), TourenDataGrid) &&
            Tabellenbedienung.SucheVorfahr<ComboBox>(e.OriginalSource) is null &&
            (_inlineDraft is null || _inlineDraft.TourId == tour.Id))
        {
            e.Handled = true;
            _inlineDialogMouseTimestamp = e.Timestamp;
            if (_inlineDialogStart || _geschlossen || _inlineDisposed) return;
            _inlineDialogStart = true;
            try
            {
                if (_inlineDraft is not null)
                {
                    BeendeNativeInlineBearbeitung();
                    if (_inlineDraft is not null) return;
                }
                _inlineUiGeneration++;
                _viewModel.VerwirfInlineEntwurf();
                if (_viewModel.AusgewaehlteTour?.Id == tour.Id && _viewModel.Touren.Any(x => ReferenceEquals(x, tour)) &&
                    _viewModel.TourBearbeitenCommand.CanExecute(null))
                    _viewModel.TourBearbeitenCommand.Execute(null);
            }
            finally { _inlineDialogStart = false; }
            return;
        }
        if (_inlineDraft is null) return;
        var cell = Tabellenbedienung.SucheVorfahr<DataGridCell>(e.OriginalSource);
        if (cell is null || !cell.IsEditing) e.Handled = true;
    }

    private void InlinePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_inlineBusy) { e.Handled = true; return; }
        if (_inlineDraft is null) return;
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            BeendeNativeInlineBearbeitung();
            _inlineUiGeneration++;
            _viewModel.VerwirfInlineEntwurf();
        }
        else if (e.Key is Key.Enter or Key.Tab)
        {
            e.Handled = true;
            TourenDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        }
        else if (e.Key == Key.Delete)
        {
            e.Handled = true;
            if (Keyboard.FocusedElement is TextBox text)
                EditingCommands.Delete.Execute(null, text);
        }
        else if (e.Key == Key.F2 || (Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.R)
            e.Handled = true;
    }

    private void DisposeInline()
    {
        if (_inlineDisposed) return;
        _inlineDisposed = true;
        _inlineUiGeneration++;
        _inlineSave?.Cancel();
        _inlineDraft = null;
        PreviewKeyDown -= InlinePreviewKeyDown;
        PreviewMouseDown -= InlinePreviewMouseDown;
        _viewModel.BeendeInline();
    }
}
