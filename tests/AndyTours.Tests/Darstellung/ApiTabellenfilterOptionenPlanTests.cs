using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiTabellenfilterOptionenPlanTests
{
    private static readonly string[] EquipmentValues = ["Bike", "(alle)", "Old (ausgemustert)"];
    private static readonly string[] ExpectedEmptyEquipment = ["Bike", "(alle)", "Old (ausgemustert)", ""];
    private static readonly string[] AreaValues = ["GA1", "ga1"];

    [Fact]
    public void Copies_options_preserves_order_and_keeps_explicit_all_distinct_from_real_all_text()
    {
        var equipments = new List<string>(EquipmentValues);
        var areas = new List<string>(AreaValues);
        var plan = ApiTabellenfilterOptionenPlan.Erstellen(
            new TourenTabellenfilterOptionenAntwort(equipments, areas), "(alle)", "ga1");
        equipments.Clear();
        areas.Clear();

        Assert.Equal(EquipmentValues, plan.Equipments.Skip(1).Select(option => option.Wert));
        Assert.Equal(AreaValues, plan.Trainingsbereiche.Skip(1).Select(option => option.Wert));
        Assert.Null(plan.Equipments[0].Wert);
        Assert.Equal("Alle Equipments", plan.Equipments[0].Name);
        Assert.NotSame(plan.Equipments[0], plan.Equipments.Single(option => option.Wert == "(alle)"));
        Assert.Same(plan.Equipments.Single(option => option.Wert == "(alle)"), plan.AusgewaehltesEquipment);
        Assert.Same(plan.Trainingsbereiche.Single(option => option.Wert == "ga1"), plan.AusgewaehlterBereich);
        Assert.Throws<NotSupportedException>(() => ((IList<ApiTabellenfilterOption>)plan.Equipments).Add(new("x", "x")));
    }

    [Fact]
    public void Null_selects_explicit_all_and_absent_nonnull_values_are_retained()
    {
        var plan = ApiTabellenfilterOptionenPlan.Erstellen(
            new TourenTabellenfilterOptionenAntwort(EquipmentValues, AreaValues), null, "retired-area");
        Assert.Same(plan.Equipments[0], plan.AusgewaehltesEquipment);
        Assert.Equal("retired-area", plan.AusgewaehlterBereich.Wert);
        Assert.Same(plan.AusgewaehlterBereich, plan.Trainingsbereiche[^1]);

        var emptyChoice = ApiTabellenfilterOptionenPlan.Erstellen(
            new TourenTabellenfilterOptionenAntwort(EquipmentValues, AreaValues), string.Empty, null);
        Assert.Equal(string.Empty, emptyChoice.AusgewaehltesEquipment.Wert);
        Assert.NotSame(emptyChoice.Equipments[0], emptyChoice.AusgewaehltesEquipment);
        Assert.Same(emptyChoice.AusgewaehltesEquipment, emptyChoice.Equipments[^1]);
        Assert.Equal(ExpectedEmptyEquipment, emptyChoice.Equipments.Skip(1).Select(option => option.Wert));
    }

    [Theory]
    [MemberData(nameof(InvalidResponses))]
    public void Rejects_invalid_option_payload_with_fixed_page_exception(TourenTabellenfilterOptionenAntwort? payload)
    {
        Assert.Throws<ApiTourSeitenException>(() => ApiTabellenfilterOptionenPlan.Erstellen(payload, null, null));
    }

    public static IEnumerable<object?[]> InvalidResponses() =>
    [
        [null],
        [new TourenTabellenfilterOptionenAntwort(null!, AreaValues)],
        [new TourenTabellenfilterOptionenAntwort(["Bike", "Bike"], AreaValues)],
        [new TourenTabellenfilterOptionenAntwort([" "], AreaValues)],
        [new TourenTabellenfilterOptionenAntwort(EquipmentValues, ["GA1", "GA1"])]
    ];
}
