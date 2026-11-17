namespace AndyTours.Tests.Sicherung;

using AndyTours.Api;
using AndyTours.Application.Sicherung;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class SicherungsarchivKonfigurationTests
{
    private static readonly (string Key, string Value)[] ValidValues =
    [
        ("Backend:Sicherung:MaxEintraege", "10"),
        ("Backend:Sicherung:MaxDateiBytes", "1000"),
        ("Backend:Sicherung:MaxGesamtBytes", "2000"),
        ("Backend:Sicherung:MaxPfadzeichen", "100"),
        ("Backend:Sicherung:MaxArchivBytes", "10000"),
        ("Backend:Sicherung:MaxManifestBytes", "1000"),
        ("Backend:Sicherung:MaxZentralverzeichnisBytes", "2000")
    ];

    [Fact]
    public void ReadsAllExplicitValuesWithoutChangingConfiguration()
    {
        var config = Build(ValidValues);
        using var owner = (IDisposable)config;
        var original = ValidValues.ToDictionary(pair => pair.Key, pair => config[pair.Key]);
        Assert.True(SicherungsarchivKonfiguration.TryLesen(config, out var limits));
        Assert.Equal(new SicherungsarchivGrenzen(new SicherungsmanifestGrenzen(10, 1000, 2000, 100), 10000, 1000, 2000), limits);
        Assert.Equal(original, ValidValues.ToDictionary(pair => pair.Key, pair => config[pair.Key]));
    }

    [Theory]
    [InlineData("Backend:Sicherung:MaxEintraege")]
    [InlineData("Backend:Sicherung:MaxDateiBytes")]
    [InlineData("Backend:Sicherung:MaxGesamtBytes")]
    [InlineData("Backend:Sicherung:MaxPfadzeichen")]
    [InlineData("Backend:Sicherung:MaxArchivBytes")]
    [InlineData("Backend:Sicherung:MaxManifestBytes")]
    [InlineData("Backend:Sicherung:MaxZentralverzeichnisBytes")]
    public void RejectsEachMissingRequiredValue(string missingKey)
    {
        var values = ValidValues.Where(pair => pair.Key != missingKey);
        AssertInvalid(values);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData(" 1")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("999999999999999999999999999999")]
    public void RejectsMalformedOrNonpositiveValues(string? value) =>
        AssertInvalid(With("Backend:Sicherung:MaxDateiBytes", value));

    [Fact]
    public void RejectsIntMaxEntryLimitBecauseInspectorNeedsOneExtraEntry() =>
        AssertInvalid(With("Backend:Sicherung:MaxEintraege", int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void AcceptsLargestSupportedEntryLimitAndPositiveBoundaries()
    {
        var values = With("Backend:Sicherung:MaxEintraege", (int.MaxValue - 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        var config = Build(values);
        using var owner = (IDisposable)config;
        Assert.True(SicherungsarchivKonfiguration.TryLesen(config, out var limits));
        Assert.Equal(int.MaxValue - 1, limits!.Manifest.MaxEintraege);
    }

    [Fact]
    public void AcceptsOneForEveryRequiredLimit()
    {
        var values = ValidValues.Select(pair => (pair.Key, Value: "1")).ToArray();
        var config = Build(values);
        using var owner = (IDisposable)config;
        Assert.True(SicherungsarchivKonfiguration.TryLesen(config, out var limits));
        Assert.Equal(new SicherungsarchivGrenzen(new SicherungsmanifestGrenzen(1, 1, 1, 1), 1, 1, 1), limits);
    }

    [Fact]
    public void RejectsManifestBeyondArrayRepresentation()
    {
        var values = With("Backend:Sicherung:MaxManifestBytes", long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        values = With(values, "Backend:Sicherung:MaxArchivBytes", long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AssertInvalid(values);
    }

    [Theory]
    [InlineData("Backend:Sicherung:MaxDateiBytes", "Backend:Sicherung:MaxGesamtBytes", "2001", "2000")]
    [InlineData("Backend:Sicherung:MaxManifestBytes", "Backend:Sicherung:MaxArchivBytes", "10001", "10000")]
    [InlineData("Backend:Sicherung:MaxZentralverzeichnisBytes", "Backend:Sicherung:MaxArchivBytes", "10001", "10000")]
    public void RejectsInconsistentBounds(string first, string second, string firstValue, string secondValue)
    {
        var values = With(first, firstValue);
        values = With(values, second, secondValue);
        AssertInvalid(values);
    }

    private static (string Key, string Value)[] With(string key, string? value) =>
        With(ValidValues, key, value);

    private static (string Key, string Value)[] With((string Key, string Value)[] values, string key, string? value) =>
        values.Where(pair => pair.Key != key).Append((key, value!)).ToArray();

    private static IConfigurationRoot Build(IEnumerable<(string Key, string Value)> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value)).Build();

    private static void AssertInvalid(IEnumerable<(string Key, string Value)> values)
    {
        var config = Build(values);
        using var owner = (IDisposable)config;
        Assert.False(SicherungsarchivKonfiguration.TryLesen(config, out var limits));
        Assert.Null(limits);
    }
}
