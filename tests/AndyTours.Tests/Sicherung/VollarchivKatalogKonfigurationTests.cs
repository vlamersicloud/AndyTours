namespace AndyTours.Tests.Sicherung;

using AndyTours.Api;
using AndyTours.Application.Sicherung;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class VollarchivKatalogKonfigurationTests
{
    private static readonly (string Key, string Value)[] ValidValues =
    [
        ("Backend:Sicherung:MaxEintraege", "10"),
        ("Backend:Sicherung:MaxDateiBytes", "1000"),
        ("Backend:Sicherung:MaxGesamtBytes", "2000"),
        ("Backend:Sicherung:MaxPfadzeichen", "100"),
        ("Backend:Sicherung:MaxArchivBytes", "10000"),
        ("Backend:Sicherung:MaxManifestBytes", "1000"),
        ("Backend:Sicherung:MaxZentralverzeichnisBytes", "2000"),
        ("Backend:Sicherung:MaxKatalogVollarchive", "8"),
        ("Backend:Sicherung:MaxKatalogNutzdatenBytes", "32768")
    ];

    [Fact]
    public void ReadsAllNineExplicitValuesWithoutChangingConfiguration()
    {
        var config = Build(ValidValues);
        using var owner = (IDisposable)config;
        var original = ValidValues.ToDictionary(pair => pair.Key, pair => config[pair.Key]);

        Assert.True(VollarchivKatalogKonfiguration.TryLesen(config, out var limits));
        Assert.Equal(
            new VollarchivKatalogGrenzen(
                new SicherungsarchivGrenzen(new SicherungsmanifestGrenzen(10, 1000, 2000, 100), 10000, 1000, 2000),
                8, 32768),
            limits);
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
    [InlineData("Backend:Sicherung:MaxKatalogVollarchive")]
    [InlineData("Backend:Sicherung:MaxKatalogNutzdatenBytes")]
    public void RejectsEveryMissingRequiredValue(string missingKey) =>
        AssertInvalid(ValidValues.Where(pair => pair.Key != missingKey));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData(" 1")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("999999999999999999999999999999999999999999")]
    public void RejectsMalformedCatalogCaps(string? value)
    {
        AssertInvalid(With("Backend:Sicherung:MaxKatalogVollarchive", value));
        AssertInvalid(With("Backend:Sicherung:MaxKatalogNutzdatenBytes", value));
    }

    [Fact]
    public void AcceptsPositiveBoundariesWithoutUsingThemAsAllocationSizes()
    {
        var values = With("Backend:Sicherung:MaxKatalogVollarchive", int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        values = With(values, "Backend:Sicherung:MaxKatalogNutzdatenBytes", long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var config = Build(values);
        using var owner = (IDisposable)config;

        Assert.True(VollarchivKatalogKonfiguration.TryLesen(config, out var limits));
        Assert.Equal(int.MaxValue, limits!.MaxVollarchive);
        Assert.Equal(long.MaxValue, limits.MaxNutzdatenBytes);
    }

    [Fact]
    public void AcceptsOneForBothCatalogCaps()
    {
        var values = With("Backend:Sicherung:MaxKatalogVollarchive", "1");
        values = With(values, "Backend:Sicherung:MaxKatalogNutzdatenBytes", "1");
        var config = Build(values);
        using var owner = (IDisposable)config;

        Assert.True(VollarchivKatalogKonfiguration.TryLesen(config, out var limits));
        Assert.Equal(1, limits!.MaxVollarchive);
        Assert.Equal(1, limits.MaxNutzdatenBytes);
    }

    private static (string Key, string Value)[] With(string key, string? value) =>
        With(ValidValues, key, value);

    private static (string Key, string Value)[] With(
        IEnumerable<(string Key, string Value)> values, string key, string? value) =>
        values.Where(pair => pair.Key != key).Append((key, value!)).ToArray();

    private static IConfigurationRoot Build(IEnumerable<(string Key, string Value)> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value))
            .Build();

    private static void AssertInvalid(IEnumerable<(string Key, string Value)> values)
    {
        var config = Build(values);
        using var owner = (IDisposable)config;
        Assert.False(VollarchivKatalogKonfiguration.TryLesen(config, out var limits));
        Assert.Null(limits);
    }
}
