using AndyTours.Application.Touren;
using AndyTours.Core.Modell;
using AndyTours.Daten;

namespace AndyTours.Tests.Daten;

public sealed class SaisonfortschrittMapperTests
{
    [Fact]
    public void Maps_all_core_fields_literally_and_preserves_inputs()
    {
        var km = new Jahreskennzahlen(2024, -12.5, 1000, 0.5, 500, -512.5, 1012.5, -22.5);
        var hm = new Jahreskennzahlen(2024, 200, 8000, 0.025, 200, -7800, 7800, 177.7);
        var count = new Jahreskennzahlen(2024, 3, 10, 0.3, 3, -7, 7, 0.2);
        var time = new Jahreskennzahlen(2024, 25.5, null, null, null, null, null, null);
        var original = new[] { km, hm, count, time };

        var result = SaisonfortschrittMapper.ToAntwort(new DateOnly(2024, 6, 30), km, hm, count, time);

        Assert.Equal(new DateOnly(2024, 6, 30), result.Stichtag);
        Assert.Equal((-12.5, 1000d, 0.5, 500d, -512.5, 1012.5, -22.5),
            (result.Kilometer.Stand, result.Kilometer.Ziel, result.Kilometer.Anteil, result.Kilometer.Soll,
                result.Kilometer.Abweichung, result.Kilometer.Rest, result.Kilometer.ProWoche));
        Assert.Equal((200d, 8000d, 0.025, 200d, -7800d, 7800d, 177.7),
            (result.Hoehenmeter.Stand, result.Hoehenmeter.Ziel, result.Hoehenmeter.Anteil, result.Hoehenmeter.Soll,
                result.Hoehenmeter.Abweichung, result.Hoehenmeter.Rest, result.Hoehenmeter.ProWoche));
        Assert.Equal(3d, result.Anzahl.Stand);
        Assert.Equal(-7d, result.Anzahl.Abweichung);
        Assert.Equal(25.5, result.FahrzeitStunden.Stand);
        Assert.Null(result.FahrzeitStunden.Ziel);
        Assert.Null(result.FahrzeitStunden.ProWoche);
        Assert.Equal(original, new[] { km, hm, count, time });
    }

    [Fact]
    public void Copies_finite_negative_values_and_zero_without_goal_reinterpretation()
    {
        var noGoal = new Jahreskennzahlen(2024, 0, null, null, null, null, null, null);
        var result = SaisonfortschrittMapper.ToAntwort(new DateOnly(2024, 1, 1), noGoal, noGoal, noGoal, noGoal);
        Assert.Equal(0, result.Kilometer.Stand);
        Assert.Null(result.Kilometer.Ziel);
        Assert.Equal(noGoal.Stand, result.FahrzeitStunden.Stand);
    }

    [Fact]
    public void Rejects_null_or_wrong_year_core_values_with_fixed_error()
    {
        var valid = new Jahreskennzahlen(2024, 1, null, null, null, null, null, null);
        AssertFixed(() => SaisonfortschrittMapper.ToAntwort(new DateOnly(2024, 1, 1), null!, valid, valid, valid));
        AssertFixed(() => SaisonfortschrittMapper.ToAntwort(new DateOnly(2024, 1, 1), valid, new Jahreskennzahlen(2023, 1, null, null, null, null, null, null), valid, valid));
    }

    [Fact]
    public void Rejects_nonfinite_values_in_each_metric_field()
    {
        var finite = new Jahreskennzahlen(2024, 1, 2, 3, 4, 5, 6, 7);
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            AssertFixed(() => SaisonfortschrittMapper.ToAntwort(new DateOnly(2024, 1, 1), finite with { Stand = bad }, finite, finite, new Jahreskennzahlen(2024, 1, null, null, null, null, null, null)));
            AssertFixed(() => SaisonfortschrittMapper.ToAntwort(new DateOnly(2024, 1, 1), finite with { Ziel = bad }, finite, finite, new Jahreskennzahlen(2024, 1, null, null, null, null, null, null)));
            AssertFixed(() => SaisonfortschrittMapper.ToAntwort(new DateOnly(2024, 1, 1), finite with { Anteil = bad }, finite, finite, new Jahreskennzahlen(2024, 1, null, null, null, null, null, null)));
            AssertFixed(() => SaisonfortschrittMapper.ToAntwort(new DateOnly(2024, 1, 1), finite with { Soll = bad }, finite, finite, new Jahreskennzahlen(2024, 1, null, null, null, null, null, null)));
            AssertFixed(() => SaisonfortschrittMapper.ToAntwort(new DateOnly(2024, 1, 1), finite with { Differenz = bad }, finite, finite, new Jahreskennzahlen(2024, 1, null, null, null, null, null, null)));
            AssertFixed(() => SaisonfortschrittMapper.ToAntwort(new DateOnly(2024, 1, 1), finite with { Rest = bad }, finite, finite, new Jahreskennzahlen(2024, 1, null, null, null, null, null, null)));
            AssertFixed(() => SaisonfortschrittMapper.ToAntwort(new DateOnly(2024, 1, 1), finite with { ProWoche = bad }, finite, finite, new Jahreskennzahlen(2024, 1, null, null, null, null, null, null)));
        }
    }

    [Fact]
    public void Rejects_each_nonnull_duration_goal_field()
    {
        var valid = new Jahreskennzahlen(2024, 1, null, null, null, null, null, null);
        var invalid = new[]
        {
            valid with { Ziel = 1 }, valid with { Anteil = 1 }, valid with { Soll = 1 },
            valid with { Differenz = 1 }, valid with { Rest = 1 }, valid with { ProWoche = 1 }
        };
        foreach (var duration in invalid)
            AssertFixed(() => SaisonfortschrittMapper.ToAntwort(new DateOnly(2024, 1, 1), valid, valid, valid, duration));
    }

    private static void AssertFixed(Action action)
    {
        var exception = Assert.Throws<UngueltigeTourException>(action);
        Assert.Equal("Die Saisonkennzahlen sind ungültig.", exception.Message);
    }
}
