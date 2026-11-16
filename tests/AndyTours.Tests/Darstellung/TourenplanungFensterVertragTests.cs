using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

public sealed class TourenplanungFensterVertragTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static XDocument Load() { using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("AndyTours.TourenplanungFenster.xaml"); Assert.NotNull(s); return XDocument.Load(s!); }

    [Fact]
    public void Api_busy_profile_retry_and_legacy_commands_are_bound()
    {
        var d = Load();
        Assert.Single(d.Descendants(P + "WrapPanel"), x => (string?)x.Attribute("IsEnabled") == "{Binding KannApiBedienen}");
        Assert.Single(d.Descendants().Where(x => x.Name.LocalName == "MapControl"), x => (string?)x.Attribute("IsEnabled") == "{Binding KannApiBedienen}");
        Assert.Single(d.Descendants(P + "TextBlock"), x => (string?)x.Attribute("Text") == "{Binding ApiProfilHinweis}");
        var retry = Assert.Single(d.Descendants(P + "Button"), x => (string?)x.Attribute("Command") == "{Binding ProfilSynchronisierenCommand}");
        Assert.Equal("{Binding IstApiModus, Converter={StaticResource SichtbarWennWahr}}", (string?)retry.Attribute("Visibility"));
        var commands = d.Descendants(P + "Button").Select(x => (string?)x.Attribute("Command")).ToArray();
        foreach (var command in new[] { "OeffnenCommand", "SpeichernCommand", "GarminUebertragenCommand", "LetztenPunktEntfernenCommand", "AllesLoeschenCommand" })
            Assert.Contains("{Binding " + command + "}", commands);
        var keys = d.Descendants(P + "Window.Resources").Elements().Select(x => (string?)x.Attribute(X + "Key")).ToHashSet(StringComparer.Ordinal);
        var refs = d.Descendants().Attributes().Select(a => a.Value).Where(v => v.Contains("{StaticResource ", StringComparison.Ordinal)).SelectMany(v => v.Split("{StaticResource ", StringSplitOptions.None).Skip(1)).Select(v => v.Split('}')[0]).ToHashSet(StringComparer.Ordinal);
        Assert.All(refs, key => Assert.Contains(key, keys)); Assert.All(keys, key => Assert.Contains(key, refs));
    }
}
