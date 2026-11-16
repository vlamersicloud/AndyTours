using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiEquipmentEinsatzViewModelTests
{
    [Fact]
    public void Maps_copies_and_sorts_each_column_in_both_directions_with_summary_always_last()
    {
        AssertColumn(Einsatzspalte.Equipment, ["A", "A2", "B", "Null", "Summe"], ["Null", "B", "A2", "A", "Summe"]);
        AssertColumn(Einsatzspalte.VorherKm, ["Null", "A", "A2", "B", "Summe"], ["B", "A", "A2", "Null", "Summe"]);
        AssertColumn(Einsatzspalte.GesamtKm, ["Null", "A", "A2", "B", "Summe"], ["B", "A", "A2", "Null", "Summe"]);
        AssertColumn(Einsatzspalte.GesamtHm, ["Null", "A", "A2", "B", "Summe"], ["B", "A", "A2", "Null", "Summe"]);
    }

    [Fact]
    public void Consecutive_sorts_retain_previous_tie_order_and_notify()
    {
        var vm = Create();
        var notified = 0;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.Zeilen)) notified++; };
        vm.Sortiere(Einsatzspalte.Equipment, false);
        vm.Sortiere(Einsatzspalte.GesamtKm, true);
        Assert.Equal(["Null", "A2", "A", "B", "Summe"], vm.Zeilen.Select(x => x.Name));
        Assert.Equal(2, notified);
        Assert.True(vm.Zeilen[^1].IstSumme);
    }

    [Fact]
    public void Empty_response_is_valid_and_invalid_summary_shapes_are_rejected()
    {
        var empty = new ApiEquipmentEinsatzViewModel(new EquipmentEinsatzAntwort(Array.Empty<EquipmentEinsatzzeileAntwort>()));
        Assert.False(empty.HatZeilen);
        Assert.Empty(empty.Zeilen);
        Assert.Throws<ArgumentException>(() => new ApiEquipmentEinsatzViewModel(new EquipmentEinsatzAntwort(new[] { Row("No total", 1, 1, 1) })));
        Assert.Throws<ArgumentException>(() => new ApiEquipmentEinsatzViewModel(new EquipmentEinsatzAntwort(new[] { new EquipmentEinsatzzeileAntwort("x", double.NaN, 1, 1, true) })));
    }

    private static void AssertColumn(Einsatzspalte column, string[] ascending, string[] descending)
    {
        var source = new List<EquipmentEinsatzzeileAntwort>
        {
            Row("B", 20, 20, 2), Row("Null", null, null, null), Row("A", 10, 10, 1), Row("A2", 10, 10, 1),
            new("Summe", 40, 40, 4, true)
        };
        var vm = new ApiEquipmentEinsatzViewModel(new EquipmentEinsatzAntwort(source));
        source.Clear();
        Assert.Equal(5, vm.Zeilen.Count);
        Assert.True(((IList<Einsatzzeile>)vm.Zeilen).IsReadOnly);
        Assert.True(vm.HatZeilen);
        Assert.Null(vm.Zeilen.Single(x => x.Name == "Null").GesamtKm);
        vm.Sortiere(column, true);
        Assert.Equal(ascending, vm.Zeilen.Select(x => x.Name));
        Assert.True(vm.Zeilen[^1].IstSumme);
        vm.Sortiere(column, false);
        Assert.Equal(descending, vm.Zeilen.Select(x => x.Name));
        Assert.True(vm.Zeilen[^1].IstSumme);
    }

    private static ApiEquipmentEinsatzViewModel Create() => new(new EquipmentEinsatzAntwort(new[]
    {
        Row("B", 20, 20, 2), Row("Null", null, null, null), Row("A", 10, 10, 1), Row("A2", 10, 10, 1),
        new EquipmentEinsatzzeileAntwort("Summe", 40, 40, 4, true)
    }));

    private static EquipmentEinsatzzeileAntwort Row(string name, double? before, double? total, int? hm) => new(name, before, total, hm, false);
}
