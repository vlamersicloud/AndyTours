using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiTourTabellenfilterViewModelTests
{
    [Fact]
    public void Request_preserves_raw_values_and_returns_null_only_when_unset()
    {
        var vm = new ApiTourTabellenfilterViewModel();
        Assert.Null(vm.ErstelleAnfrage());
        vm.DistanzVon = " 1,25 ";
        vm.DistanzBis = "ungültig";
        vm.HoehenmeterVon = "0";
        vm.Suchbegriff = "  Rad   Tour ";
        var request = vm.ErstelleAnfrage()!;
        Assert.Equal(" 1,25 ", request.DistanzVon);
        Assert.Equal("ungültig", request.DistanzBis);
        Assert.Equal("0", request.HoehenmeterVon);
        Assert.Equal("  Rad   Tour ", request.Suchbegriff);
    }

    [Fact]
    public void Selection_property_changes_raise_own_notification()
    {
        var vm = new ApiTourTabellenfilterViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        vm.AusgewaehltesEquipment = new ApiTabellenfilterOption("Bike", "Bike");
        vm.AusgewaehlterBereich = new ApiTabellenfilterOption("GA1", "GA1");
        Assert.Contains(nameof(vm.AusgewaehltesEquipment), changed);
        Assert.Contains(nameof(vm.AusgewaehlterBereich), changed);
    }

    [Fact]
    public void Explicit_empty_equipment_value_is_a_set_filter()
    {
        var vm = new ApiTourTabellenfilterViewModel
        {
            AusgewaehltesEquipment = new ApiTabellenfilterOption(string.Empty, string.Empty)
        };
        var request = vm.ErstelleAnfrage();
        Assert.NotNull(request);
        Assert.Equal(string.Empty, request.EquipmentName);
    }

    [Fact]
    public void Null_selection_is_rejected_without_changing_current_value()
    {
        var vm = new ApiTourTabellenfilterViewModel();
        var current = vm.AusgewaehltesEquipment;
        Assert.Throws<ArgumentNullException>(() => vm.AusgewaehltesEquipment = null!);
        Assert.Same(current, vm.AusgewaehltesEquipment);
    }

    [Fact]
    public void Range_errors_follow_shared_core_parser_including_inverted_bounds()
    {
        var vm = new ApiTourTabellenfilterViewModel { DistanzVon = "bad", HoehenmeterVon = "10", HoehenmeterBis = "2" };
        Assert.True(vm.DistanzVonFehlerhaft);
        Assert.False(vm.DistanzBisFehlerhaft);
        Assert.True(vm.HoehenmeterVonFehlerhaft);
        Assert.True(vm.HoehenmeterBisFehlerhaft);
        Assert.True(vm.DistanzbereichUngueltig);
        Assert.True(vm.HoehenmeterbereichUngueltig);
    }

    [Fact]
    public void Reset_emits_one_change_and_keeps_current_options()
    {
        var vm = new ApiTourTabellenfilterViewModel();
        var plan = ApiTabellenfilterOptionenPlan.Erstellen(
            new TourenTabellenfilterOptionenAntwort(["Bike"], ["GA1"]), null, null);
        vm.Uebernehmen(plan);
        var events = 0;
        vm.Geaendert += () => events++;
        vm.AusgewaehltesEquipment = vm.Equipments[1];
        vm.AusgewaehlterBereich = vm.Trainingsbereiche[1];
        vm.DistanzVon = "1";
        vm.DistanzBis = "2";
        vm.HoehenmeterVon = "3";
        vm.HoehenmeterBis = "4";
        vm.Suchbegriff = "find";
        events = 0;
        vm.ResetZuruecksetzenCommand.Execute(null);
        Assert.Equal(1, events);
        Assert.Equal("Bike", vm.Equipments[1].Wert);
        Assert.Equal("GA1", vm.Trainingsbereiche[1].Wert);
        Assert.Null(vm.AusgewaehltesEquipment.Wert);
        Assert.Null(vm.AusgewaehlterBereich.Wert);
        Assert.False(vm.DistanzVonFehlerhaft);
        Assert.False(vm.DistanzBisFehlerhaft);
        Assert.False(vm.HoehenmeterVonFehlerhaft);
        Assert.False(vm.HoehenmeterBisFehlerhaft);
        Assert.Null(vm.ErstelleAnfrage());
    }

    [Fact]
    public void Applying_plan_suppresses_change_event_preserves_text_and_replaces_equal_references()
    {
        var vm = new ApiTourTabellenfilterViewModel { Suchbegriff = "keep" };
        vm.Uebernehmen(ApiTabellenfilterOptionenPlan.Erstellen(
            new TourenTabellenfilterOptionenAntwort(["Bike"], ["GA1"]), "Bike", "GA1"));
        var events = 0;
        vm.Geaendert += () => events++;
        var newPlan = ApiTabellenfilterOptionenPlan.Erstellen(
            new TourenTabellenfilterOptionenAntwort(["Bike"], ["GA1"]), "Bike", "GA1");
        vm.Uebernehmen(newPlan);
        Assert.Equal(0, events);
        Assert.Equal("keep", vm.Suchbegriff);
        Assert.Same(vm.Equipments.Single(option => option.Wert == "Bike"), vm.AusgewaehltesEquipment);
        Assert.Same(vm.Trainingsbereiche.Single(option => option.Wert == "GA1"), vm.AusgewaehlterBereich);
        Assert.Same(newPlan.AusgewaehltesEquipment, vm.AusgewaehltesEquipment);
    }
}
