using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Daten;

public sealed class TourenMehrfachRegelnTests
{
    private static readonly string LowerVersion = new('a', 64);
    private static readonly string UpperVersion = new('A', 64);

    [Fact]
    public void Category_request_is_sorted_canonical_and_defensively_readonly()
    {
        var selected = new List<TourVersionsreferenz>
        {
            new(9, LowerVersion), new(2, UpperVersion)
        };
        var set = new List<long> { 8, 3 };
        var remove = new List<long> { 12, 4 };
        var id = Guid.NewGuid();
        var result = TourenMehrfachRegeln.Normalisieren(new TourenKategorienAnfrage(id, selected, set, remove));

        Assert.Equal(id, result.AnfrageId);
        Assert.Equal(new long[] { 2, 9 }, result.Touren.Select(x => x.Id));
        Assert.All(result.Touren, x => Assert.Equal(UpperVersion, x.Version));
        Assert.Equal(new long[] { 3, 8 }, result.Setzen);
        Assert.Equal(new long[] { 4, 12 }, result.Entfernen);

        selected.Clear(); set.Clear(); remove.Clear();
        Assert.Equal(new long[] { 2, 9 }, result.Touren.Select(x => x.Id));
        Assert.Equal(new long[] { 3, 8 }, result.Setzen);
        Assert.Equal(new long[] { 4, 12 }, result.Entfernen);
        Assert.Throws<NotSupportedException>(() => ((IList<long>)result.Setzen)[0] = 99);
        Assert.Throws<NotSupportedException>(() => ((IList<TourVersionsreferenz>)result.Touren).Clear());
    }

    [Fact]
    public void Every_operation_preserves_identity_targets_and_canonical_references()
    {
        var id = Guid.NewGuid();
        var references = new List<TourVersionsreferenz> { new(7, LowerVersion), new(1, UpperVersion) };
        var activity = TourenMehrfachRegeln.Normalisieren(new TourenAktivitaetAnfrage(id, references, 11));
        var equipment = TourenMehrfachRegeln.Normalisieren(new TourenEquipmentAnfrage(id, references, 15));
        var noEquipment = TourenMehrfachRegeln.Normalisieren(new TourenEquipmentAnfrage(id, references, null));
        var delete = TourenMehrfachRegeln.Normalisieren(new TourenLoeschenAnfrage(id, references));

        foreach (var refs in new[] { activity.Touren, equipment.Touren, noEquipment.Touren, delete.Touren })
        {
            Assert.Equal(new long[] { 1, 7 }, refs.Select(x => x.Id));
            Assert.All(refs, x => Assert.Equal(UpperVersion, x.Version));
        }
        Assert.Equal(id, activity.AnfrageId);
        Assert.Equal(11, activity.AktivitaetId);
        Assert.Equal(id, equipment.AnfrageId);
        Assert.Equal(15, equipment.EquipmentId);
        Assert.Null(noEquipment.EquipmentId);
        Assert.Equal(id, delete.AnfrageId);
        references[0] = new(99, LowerVersion);
        Assert.Equal(new long[] { 1, 7 }, delete.Touren.Select(x => x.Id));
    }

    [Fact]
    public void Invalid_requests_fail_with_one_fixed_safe_message()
    {
        var validRef = new TourVersionsreferenz(1, UpperVersion);
        var id = Guid.NewGuid();
        var invalid = new Action[]
        {
            () => TourenMehrfachRegeln.Normalisieren((TourenKategorienAnfrage?)null),
            () => TourenMehrfachRegeln.Normalisieren(new TourenKategorienAnfrage(Guid.Empty, [validRef], [], [])),
            () => TourenMehrfachRegeln.Normalisieren(new TourenKategorienAnfrage(id, [], [], [])),
            () => TourenMehrfachRegeln.Normalisieren(new TourenKategorienAnfrage(id, [validRef, validRef], [], [])),
            () => TourenMehrfachRegeln.Normalisieren(new TourenKategorienAnfrage(id, [null!], [], [])),
            () => TourenMehrfachRegeln.Normalisieren(new TourenKategorienAnfrage(id, [validRef], null!, [])),
            () => TourenMehrfachRegeln.Normalisieren(new TourenKategorienAnfrage(id, [validRef], [], null!)),
            () => TourenMehrfachRegeln.Normalisieren(new TourenKategorienAnfrage(id, [new(0, UpperVersion)], [], [])),
            () => TourenMehrfachRegeln.Normalisieren(new TourenKategorienAnfrage(id, [new(2, "not-a-hash")], [], [])),
            () => TourenMehrfachRegeln.Normalisieren(new TourenKategorienAnfrage(id, [validRef], [2, 2], [])),
            () => TourenMehrfachRegeln.Normalisieren(new TourenKategorienAnfrage(id, [validRef], [2], [2])),
            () => TourenMehrfachRegeln.Normalisieren(new TourenAktivitaetAnfrage(id, [validRef], 0)),
            () => TourenMehrfachRegeln.Normalisieren(new TourenEquipmentAnfrage(id, [validRef], 0)),
            () => TourenMehrfachRegeln.Normalisieren(new TourenLoeschenAnfrage(id, [new(1, new string('G', 64))]))
        };
        foreach (var action in invalid)
        {
            var error = Assert.Throws<UngueltigeTourException>(action);
            Assert.Equal("Mehrfachaktion ist ungültig.", error.Message);
        }
    }
}
