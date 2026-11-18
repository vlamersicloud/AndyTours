using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Sicherung;

public sealed class VollarchivAufbewahrungsPlanerTests
{
    [Fact]
    public void RetainsConfiguredCountAndNeverSuggestsLegacyOrInvalidEntries()
    {
        var entries = new[]
        {
            Full(1, 2026, 1), Full(2, 2025, 1), Full(3, 2024, 1),
            Legacy(4), new SicherungAntwort(Guid.Empty, Sicherungsart.Vollarchiv, DateTimeOffset.UtcNow, 10, Hash(5))
        };
        var plan = VollarchivAufbewahrungsPlaner.Erstelle(entries, 1, entries[0]);
        Assert.NotNull(plan);
        Assert.Equal(new[] { GuidFor(1) }, plan.Behalten.Select(x => x.Id));
        Assert.Equal(new[] { GuidFor(2), GuidFor(3) }, plan.VorgeschlageneKandidaten.Select(x => x.Id));
        Assert.DoesNotContain(plan.VorgeschlageneKandidaten, x => x.Art != Sicherungsart.Vollarchiv);
    }

    [Fact]
    public void Count99KeepsAllAndFreshOlderThanRetentionWindowIsStillKept()
    {
        var entries = Enumerable.Range(1, 100).Select(i => Full(i, 2026, i)).ToArray();
        var fresh = entries[0];
        var plan = VollarchivAufbewahrungsPlaner.Erstelle(entries, 99, fresh);
        Assert.NotNull(plan);
        Assert.Equal(99, plan.Behalten.Count);
        Assert.Contains(plan.Behalten, x => x.Id == fresh.Id);
        Assert.Single(plan.VorgeschlageneKandidaten);
        Assert.Equal(GuidFor(2), Assert.Single(plan.VorgeschlageneKandidaten).Id);
    }

    [Fact]
    public void TiesAreStableAndPermutationDoesNotChangePlan()
    {
        var entries = new[] { Full(3, 2026, 1), Full(1, 2026, 1), Full(2, 2025, 1) };
        var first = VollarchivAufbewahrungsPlaner.Erstelle(entries, 2, entries[1]);
        var second = VollarchivAufbewahrungsPlaner.Erstelle(entries.Reverse().ToArray(), 2, entries[1]);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Behalten.Select(x => x.Id), second.Behalten.Select(x => x.Id));
        Assert.Equal(new[] { GuidFor(1), GuidFor(3) }, first.Behalten.Select(x => x.Id));
    }

    [Fact]
    public void DuplicateIdsMissingFreshAndMismatchedFreshFailClosed()
    {
        var fresh = Full(1, 2026, 1);
        Assert.Null(VollarchivAufbewahrungsPlaner.Erstelle([fresh, fresh with { InhaltHash = Hash(2) }], 1, fresh));
        Assert.Null(VollarchivAufbewahrungsPlaner.Erstelle([Full(2, 2025, 1)], 1, fresh));
        Assert.Null(VollarchivAufbewahrungsPlaner.Erstelle([fresh], 1, fresh with { Byteanzahl = 99 }));
        Assert.Null(VollarchivAufbewahrungsPlaner.Erstelle([fresh], 0, fresh));
    }

    [Fact]
    public void OutputCollectionsAreReadOnlyAndInputMutationCannotChangePlan()
    {
        var input = new List<SicherungAntwort> { Full(1, 2026, 1), Full(2, 2025, 1) };
        var plan = VollarchivAufbewahrungsPlaner.Erstelle(input, 1, input[0]);
        Assert.NotNull(plan);
        input.Clear();
        Assert.Single(plan.Behalten);
        Assert.Throws<NotSupportedException>(() => ((IList<SicherungAntwort>)plan.Behalten).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<SicherungAntwort>)plan.VorgeschlageneKandidaten).Clear());
    }

    [Fact]
    public void InvalidBoundsNullInputsAndInvalidFreshFailClosed()
    {
        var fresh = Full(1, 2026, 1);
        Assert.Null(VollarchivAufbewahrungsPlaner.Erstelle([fresh], 100, fresh));
        Assert.Null(VollarchivAufbewahrungsPlaner.Erstelle(null, 1, fresh));
        Assert.Null(VollarchivAufbewahrungsPlaner.Erstelle([fresh], 1, null));
        Assert.Null(VollarchivAufbewahrungsPlaner.Erstelle([fresh], 1,
            fresh with { ErstelltUtc = null }));
    }

    private static SicherungAntwort Full(int id, int year, int second) =>
        new(GuidFor(id), Sicherungsart.Vollarchiv,
            new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(second), 1024, Hash(id));

    private static SicherungAntwort Legacy(int id) =>
        new(GuidFor(id), Sicherungsart.LegacySqlite, null, 10, Hash(id)) { LegacyZeitstempel = "2020-01-01_00-00-00" };

    private static Guid GuidFor(int value) => new(value, 0, 0, new byte[8]);
    private static string Hash(int value) => value.ToString("X64", System.Globalization.CultureInfo.InvariantCulture);
}
