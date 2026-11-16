using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;

namespace AndyTours.Tests.Daten;

public sealed class TourenTabellenfilterOptionenMapperTests
{
    private static readonly string[] ExpectedEquipments = ["Road", "Old (ausgemustert)", "(alle)"];
    private static readonly string[] ExpectedAreas = ["GA1", "GA 2"];

    [Fact]
    public void Copies_exact_order_and_text_without_placeholder_and_isolates_mutable_inputs()
    {
        var equipments = new List<string> { Filterwerte.Alle, "Road", "Old (ausgemustert)", "(alle)" };
        var areas = new List<string> { Filterwerte.Alle, "GA1", "GA 2" };
        var source = new Filterwerte(new List<string> { Filterwerte.Alle }, equipments, areas);

        TourenTabellenfilterOptionenAntwort mapped = TourenTabellenfilterOptionenMapper.ToAntwort(source);
        equipments.Clear();
        areas.Clear();

        Assert.Equal(ExpectedEquipments, mapped.Equipments);
        Assert.Equal(ExpectedAreas, mapped.Trainingsbereiche);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)mapped.Equipments).Add("mutate"));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)mapped.Trainingsbereiche).Add("mutate"));
    }

    [Fact]
    public void Core_lists_containing_only_all_map_to_empty_text_options()
    {
        var mapped = TourenTabellenfilterOptionenMapper.ToAntwort(
            new Filterwerte([Filterwerte.Alle], [Filterwerte.Alle], [Filterwerte.Alle]));
        Assert.Empty(mapped.Equipments);
        Assert.Empty(mapped.Trainingsbereiche);
    }

    [Theory]
    [MemberData(nameof(InvalidLists))]
    public void Rejects_invalid_option_lists_with_fixed_safe_error(IReadOnlyList<string>? values)
    {
        var source = new Filterwerte([Filterwerte.Alle], values!, [Filterwerte.Alle]);
        var error = Assert.Throws<UngueltigeTourException>(() => TourenTabellenfilterOptionenMapper.ToAntwort(source));
        Assert.Equal("Tabellenfilteroptionen sind ungültig.", error.Message);
    }

    public static IEnumerable<object?[]> InvalidLists() =>
    [
        [null],
        [Array.Empty<string>()],
        [(string[]) ["Road"]],
        [(string[]) [Filterwerte.Alle, "Road", "Road"]],
        [(string[]) [Filterwerte.Alle, "   "]]
    ];
}
