using System.Reflection;
using System.Xml.Linq;
namespace AndyTours.Tests.Berechnungen;

public sealed class ApiTourInlineStrukturTests
{
    private static readonly string[] ReadonlyPfade = ["Anzeige.DurchschnittKmh", "Anzeige.Kategorien", "Anzeige.Kommentar"];
    private static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AndyTours." + name)
            ?? throw new InvalidOperationException(name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Achtzehn_Sortpfade_drei_Readonly_und_drei_ComboTemplates()
    {
        var document = XDocument.Parse(Resource("ApiHauptfenster.xaml"));
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var grid = document.Descendants(wpf + "DataGrid").Single(e => (string?)e.Attribute(x + "Name") == "TourenDataGrid");
        Assert.Equal("False", (string?)grid.Attribute("IsReadOnly"));
        var columns = grid.Element(wpf + "DataGrid.Columns")!.Elements().ToArray();
        Assert.Equal(18, columns.Length);
        Assert.Equal(18, columns.Select(c => (string?)c.Attribute("SortMemberPath")).Distinct().Count());
        Assert.All(columns, c => Assert.StartsWith("Anzeige.", (string?)c.Attribute("SortMemberPath") ?? ""));
        var readonlyPaths = columns.Where(c => (string?)c.Attribute("IsReadOnly") == "True")
            .Select(c => (string?)c.Attribute("SortMemberPath")).OrderBy(c => c).ToArray();
        Assert.Equal(ReadonlyPfade, readonlyPaths);
        var combos = columns.Where(c => c.Descendants(wpf + "ComboBox").Any()).ToArray();
        Assert.Equal(3, combos.Length);
        Assert.All(combos, c => Assert.NotNull(c.Element(wpf + "DataGridTemplateColumn.CellEditingTemplate")));
        Assert.Equal(12, columns.Count(c => c.Name.LocalName == "Zelltextspalte"));
        Assert.DoesNotContain(grid.Descendants(wpf + "KeyBinding"), b => (string?)b.Attribute("Key") is "F2" or "Enter");
        Assert.Equal("InlinePreparingCellForEdit", (string?)grid.Attribute("PreparingCellForEdit"));
    }

    [Fact]
    public void Urspruengliche_Mehrfachhandler_sperren_Inline_ohne_Snapshotregeln_zu_aendern()
    {
        var source = Resource("ApiHauptfenster.Mehrfach.cs");
        Assert.Contains("_inlineBusy || _inlineDraft is not null ||", source);
        Assert.Contains("_viewModel.InlineGesperrt || _verwaltungOffen", source);
        Assert.Contains("if (_geschlossen || _inlineDisposed || _inlineBusy || _inlineDraft is not null || _viewModel.InlineGesperrt) return;", source);
        Assert.Contains("!_geschlossen && !_inlineDisposed && IsVisible", source);
        Assert.Contains("ErfasseTourenauswahl().Select(Abbild).ToArray()", source);
        Assert.Contains("callback(Array.AsReadOnly(selected), token)", source);
        Assert.Contains("if (outcome == ApiTourenMehrfachStatus.Bestaetigt)", source);
        Assert.Contains("_viewModel.EntferneBestaetigteTouren(selected.Select(t => t.Id))", source);
        Assert.Contains("(long[])tour.Werte.KategorieIds.Clone()", source);
    }

    [Fact]
    public void Erstzeichen_Doppelklick_und_DeferredFilter_sind_explizit_koordiniert()
    {
        var ui = Resource("ApiHauptfenster.Inline.cs");
        Assert.Contains("e.EditingEventArgs is not TextCompositionEventArgs", ui);
        Assert.Contains("composition.Text : null", ui);
        Assert.Contains("editor.Text = initialText", ui);
        Assert.Contains("e.ClickCount == 2", ui);
        Assert.Contains("(_inlineDraft is null || _inlineDraft.TourId == tour.Id)", ui);
        Assert.Contains("SucheVorfahr<ComboBox>(e.OriginalSource) is null", ui);
        Assert.Contains("ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(row), TourenDataGrid)", ui);
        Assert.Contains("if (_inlineDraft is not null) return;", ui);
        Assert.Contains("_inlineDialogStart = true", ui);
        Assert.Contains("_inlineDialogMouseTimestamp = e.Timestamp", ui);
        Assert.Contains("if (e.Timestamp == _inlineDialogMouseTimestamp)", Resource("ApiHauptfenster.xaml.cs"));
        Assert.Contains("_viewModel.InlineFilterAenderungAusstehend", ui);
        var vm = Resource("ApiUebersichtViewModel.Inline.cs");
        Assert.Contains("OnPropertyChanged(nameof(HatTourenauswahl))", vm);
        Assert.Contains("private void BenachrichtigeInlineZustand()", vm);
        Assert.Contains("private bool MerkeInlineFilterAenderung(bool nurTabelle)", vm);
        Assert.Contains("InlineNeuLadenNoetig = true", vm);
        Assert.Contains("Interlocked.Increment(ref _generation)", vm);
        Assert.Contains("if (!aktiv) WendeInlineFilterInvalidierungAn()", vm);
        Assert.Contains("InlineFilterAenderungAusstehend || InlineNeuLadenNoetig", vm);
        Assert.Contains("snapshot.Aktivitaeten.Any(x => x is null || x.Id <= 0 || string.IsNullOrWhiteSpace(x.Name))", vm);
        Assert.Contains("snapshot.Equipments.Any(x => x is null || x.Id <= 0 || string.IsNullOrWhiteSpace(x.Name))", vm);
        var main = Resource("ApiUebersichtViewModel.cs");
        Assert.Contains("MerkeInlineFilterAenderung(nurTabelle: true)", main);
        Assert.Contains("MerkeInlineFilterAenderung(nurTabelle: false)", main);
        Assert.Contains("InlineNeuLadenNoetig = false", main);
    }

    [Fact]
    public void Savequelle_laedt_nicht_neu_und_UI_beendet_native_Bearbeitung_vor_Refresh()
    {
        var vm = Resource("ApiUebersichtViewModel.Inline.cs");
        Assert.DoesNotContain("await AktualisierenAsync", vm);
        var ui = Resource("ApiHauptfenster.Inline.cs");
        Assert.Contains("ReferenceEquals(_inlineSave, saveSource)", ui);
        Assert.Contains("saveSource.Dispose()", ui);
        Assert.DoesNotContain("_inlineSave?.Dispose()", ui);
        Assert.DoesNotContain("DataGridBoundColumn", ui);
        Assert.Contains("FindeInlineCombo(e.EditingElement)", ui);
        Assert.Contains("x.Name == _inlineDraft.Werte.Trainingsbereich", ui);
        Assert.Contains("BeendeNativeInlineBearbeitung();", ui);
        Assert.Contains("saved && !_geschlossen && _inlineDraft is null", ui);
        Assert.Contains("await _viewModel.AktualisierenAsync", ui);
        Assert.Contains("if (_inlineDisposed) return;", ui);
    }
}
