using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests;

public sealed class ApiTourenFilterauswahlCommitTests
{
    private static readonly TourenFilterauswahlAnfrage Filter = new(
        new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 2, null, null);

    private static TourAntwort Tour(long id, char version = 'A', params long[] categories) =>
        new(id, new string(version, 64),
            new TourWerte(1, new DateOnly(2026, 4, 12), null, $"Tour{id}", 60,
                1, 0, 100, 0, 10, 100, 120, 80, 60, 10, 0, 60, 0,
                "GA1", "", 2, null, categories));

    [Fact]
    public void MatchingFilterAndGenerationAtomicallyReplaceVersionedRows()
    {
        var selection = new ApiTourenauswahl();
        selection.Setze(Tour(1, 'A', 9), true);
        var incoming = new[] { Tour(2, 'B', 3, 7), Tour(3, 'C', 11) };
        var response = new TourenFilterauswahlAntwort(Filter, incoming);

        Assert.True(ApiTourenFilterauswahlCommit.TryCommit(
            selection, response, Filter, "applied", "applied", 4, 4, closed: false));
        incoming[0].Werte.KategorieIds[0] = 999;
        Assert.Equal(new long[] { 2, 3 }, selection.Snapshot.Select(x => x.Id));
        Assert.Equal(new string('B', 64), selection.Snapshot[0].Version);
        Assert.Equal(new long[] { 3, 7 }, selection.Snapshot[0].Werte.KategorieIds);
    }

    [Fact]
    public void FilterMismatchStaleGenerationCloseAndInvalidCandidatePreserveOldSnapshot()
    {
        var selection = new ApiTourenauswahl();
        selection.Setze(Tour(1, 'A', 5), true);
        var valid = new TourenFilterauswahlAntwort(Filter, [Tour(2, 'B')]);

        Assert.False(ApiTourenFilterauswahlCommit.TryCommit(
            selection, valid with { Filter = Filter with { KategorieId = 6 } },
            Filter, "applied", "applied", 4, 4, closed: false));
        Assert.False(ApiTourenFilterauswahlCommit.TryCommit(
            selection, valid, Filter, "applied", "applied", 4, 5, closed: false));
        Assert.False(ApiTourenFilterauswahlCommit.TryCommit(
            selection, valid, Filter, "applied", "applied", 4, 4, closed: true));
        Assert.False(ApiTourenFilterauswahlCommit.TryCommit(
            selection, valid with { Touren = [Tour(3, 'C'), Tour(3, 'D')] },
            Filter, "applied", "applied", 4, 4, closed: false));

        Assert.Equal(new long[] { 1 }, selection.Snapshot.Select(x => x.Id));
        Assert.Equal(new string('A', 64), selection.Snapshot.Single().Version);
        Assert.Equal(new long[] { 5 }, selection.Snapshot.Single().Werte.KategorieIds);
    }

    [Fact]
    public void NullAndChangedAppliedQueryAreNotCommitted()
    {
        var selection = new ApiTourenauswahl();
        selection.Setze(Tour(9, 'F'), true);
        Assert.False(ApiTourenFilterauswahlCommit.TryCommit(
            selection, null, Filter, "applied", "applied", 1, 1, closed: false));
        Assert.False(ApiTourenFilterauswahlCommit.TryCommit(
            selection, new TourenFilterauswahlAntwort(Filter, [Tour(10)]),
            Filter, "old", "new", 1, 1, closed: false));
        Assert.Equal(new long[] { 9 }, selection.Snapshot.Select(x => x.Id));
    }
}
