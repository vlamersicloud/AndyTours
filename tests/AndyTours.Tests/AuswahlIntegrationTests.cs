using System.Reflection;

namespace AndyTours.Tests;

public sealed class AuswahlIntegrationTests
{
    private static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AndyTours." + name);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }

    [Fact]
    public void BulkActionUsesFrozenVmSnapshotAndOnlyConfirmedOutcomeRemovesIds()
    {
        var bulk = Resource("ApiHauptfenster.Mehrfach.cs");
        Assert.Contains("ErfasseTourenauswahl()", bulk, StringComparison.Ordinal);
        Assert.DoesNotContain("TourenDataGrid.SelectedItems.OfType<TourAntwort>()", bulk, StringComparison.Ordinal);
        Assert.Contains("outcome == ApiTourenMehrfachStatus.Bestaetigt", bulk, StringComparison.Ordinal);
        Assert.Contains("EntferneBestaetigteTouren(selected.Select(t => t.Id))", bulk, StringComparison.Ordinal);
        var tours = Resource("ApiUebersichtViewModel.Touren.cs");
        Assert.Contains("snapshot.Count == 1", tours, StringComparison.Ordinal);
        Assert.Contains("snapshot[0].Id == AusgewaehlteTour.Id", tours, StringComparison.Ordinal);
    }

    [Fact]
    public void GridSelectionRestoresSnapshotWithGuardAndDetaches()
    {
        var grid = Resource("ApiHauptfenster.ManuelleAuswahl.cs");
        Assert.Contains("_auswahlAnwenden", grid, StringComparison.Ordinal);
        Assert.Contains("StelleSichtbareAuswahlWiederHer", grid, StringComparison.Ordinal);
        Assert.Contains("ErfasseTourenauswahl()", grid, StringComparison.Ordinal);
        Assert.Contains("DisposeManuelleAuswahl", grid, StringComparison.Ordinal);
        Assert.Contains("PropertyChanged -= ManuelleAuswahlPropertyChanged", grid, StringComparison.Ordinal);
        Assert.Contains("CollectionChanged -= ManuelleAuswahlCollectionChanged", grid, StringComparison.Ordinal);
        var handler = Resource("ApiHauptfenster.Auswahl.cs");
        Assert.Contains("VerarbeiteManuelleTourenauswahl(sender, e)", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("SetzeTourenauswahl(grid.SelectedItems.Count)", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void QuerySelectionHelperRunsAfterValidationInAllThreeCommitPaths()
    {
        var main = Resource("ApiUebersichtViewModel.cs");
        var sort = Resource("ApiUebersichtViewModel.Sortierung.cs");
        Assert.Contains("private bool UebernehmeAuswahlQuery(TourenListenfilter filter)", main, StringComparison.Ordinal);
        Assert.Contains("selectionFilterChanged = UebernehmeAuswahlQuery(filter)", main, StringComparison.Ordinal);
        Assert.Contains("var selectionFilterChanged = UebernehmeAuswahlQuery(filter)", main, StringComparison.Ordinal);
        Assert.Contains("var selectionFilterChanged = UebernehmeAuswahlQuery(filter)", sort, StringComparison.Ordinal);
        Assert.True(main.IndexOf("ApiTourSeitenRegeln.Validieren(page, start, 50)", StringComparison.Ordinal)
            < main.IndexOf("var selectionFilterChanged = UebernehmeAuswahlQuery(filter)", StringComparison.Ordinal));
        Assert.True(sort.IndexOf("ValidiereSortierantwort(page)", StringComparison.Ordinal)
            < sort.IndexOf("var selectionFilterChanged = UebernehmeAuswahlQuery(filter)", StringComparison.Ordinal));
    }
}
