using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests;

public sealed class ApiTourenauswahlTests
{
    private static TourAntwort Tour(long id, char version, params long[] categories) =>
        new(id, new string(version, 64), new TourWerte(1, new DateOnly(2026, 1, 1), null, $"Tour{id}", 100, 5, 10, 20, 0, 20, 100, 120, 80, 80, 10, 0, 70, 0, "", "", 1, null, categories));

    [Fact]
    public void PageReconcilePreservesOffPageAndExplicitlyDeselectionOnlyRemovesVisibleRows()
    {
        var selection = new ApiTourenauswahl();
        selection.SetzeSichtbare([Tour(1, 'A'), Tour(2, 'B')], [1, 2]);
        selection.SetzeSichtbare([Tour(3, 'C')], [3]);
        Assert.Equal(new long[] { 1, 2, 3 }, selection.Snapshot.Select(x => x.Id));
        selection.SetzeSichtbare([Tour(3, 'D')], []);
        Assert.Equal(new long[] { 1, 2 }, selection.Snapshot.Select(x => x.Id));
    }

    [Fact]
    public void SelectedSnapshotDeepCopiesCategoriesAndFreezesVersionAcrossReload()
    {
        var selection = new ApiTourenauswahl();
        var categoryIds = new long[] { 7, 9 };
        var first = Tour(4, 'A', categoryIds);
        selection.Setze(first, true);
        categoryIds[0] = 100;
        selection.SetzeSichtbare([Tour(4, 'B', 11)], [4]);
        var copy = selection.Snapshot[0];
        Assert.Equal(new string('A', 64), copy.Version);
        Assert.Equal(new long[] { 7, 9 }, copy.Werte.KategorieIds);
        copy.Werte.KategorieIds[0] = 55;
        Assert.Equal(new long[] { 7, 9 }, selection.Snapshot[0].Werte.KategorieIds);
    }

    [Fact]
    public void DirectSetFreezesVersionAndDeselectThenReselectRefreshesIt()
    {
        var selection = new ApiTourenauswahl();
        selection.Setze(Tour(8, 'A'), true);
        selection.Setze(Tour(8, 'B'), true);
        Assert.Equal(new string('A', 64), selection.Snapshot.Single().Version);
        selection.Setze(Tour(8, 'C'), false);
        selection.Setze(Tour(8, 'D'), true);
        Assert.Equal(new string('D', 64), selection.Snapshot.Single().Version);
    }

    [Fact]
    public void DuplicateRowsAndSelectedIdsFailAtomically()
    {
        var selection = new ApiTourenauswahl();
        selection.Setze(Tour(1, 'A'), true);
        Assert.Throws<ArgumentException>(() => selection.SetzeSichtbare([Tour(1, 'B'), Tour(1, 'C')], []));
        Assert.Throws<ArgumentException>(() => selection.SetzeSichtbare([Tour(2, 'B')], [2, 2]));
        Assert.Equal(new string('A', 64), selection.Snapshot.Single().Version);
        Assert.Equal(new long[] { 1 }, selection.Snapshot.Select(x => x.Id));
    }

    [Fact]
    public void ClearAndConfirmedIdsAreExactAndInvalidRowsRejected()
    {
        var selection = new ApiTourenauswahl();
        selection.Setze(Tour(1, 'A'), true);
        selection.Setze(Tour(2, 'B'), true);
        selection.EntferneBestaetigte([2]);
        Assert.Equal(new long[] { 1 }, selection.Snapshot.Select(x => x.Id));
        Assert.Throws<ArgumentException>(() => selection.Setze(Tour(0, 'A'), true));
        Assert.Throws<ArgumentException>(() => selection.Setze(Tour(3, 'z'), true));
        selection.Clear();
        Assert.Equal(0, selection.Count);
    }
}
