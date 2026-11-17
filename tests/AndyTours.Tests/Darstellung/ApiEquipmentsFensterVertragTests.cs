using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

/// <summary>Strukturelle Regressionen für Equipmentfenster-Ressourcen und Aktivitätsnamenbindung.</summary>
public sealed class ApiEquipmentsFensterVertragTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void JedeStaticResourceIstImFensterUndInAppDictionariesDefiniertUndAktivitaetsConverterIstGebunden()
    {
        var document = Load("AndyTours.ApiEquipmentsFenster.xaml");
        var localKeys = document.Descendants(Presentation + "Window.Resources").Elements()
            .Select(element => (string?)element.Attribute(Xaml + "Key")).Where(key => key is not null).ToHashSet(StringComparer.Ordinal);
        var sharedKeys = Keys(Load("AndyTours.App.xaml"));
        sharedKeys.UnionWith(Keys(Load("AndyTours.ApiOberflaeche.xaml")));
        Assert.Contains("EquipmentAktivitaetConverter", localKeys);
        var refs = document.Descendants().Attributes().Select(a => a.Value)
            .Where(v => v.Contains("{StaticResource ", StringComparison.Ordinal))
            .SelectMany(v => v.Split("{StaticResource ", StringSplitOptions.None).Skip(1))
            .Select(v => v.Split('}')[0]).ToHashSet(StringComparer.Ordinal);
        Assert.All(refs, key => Assert.True(localKeys.Contains(key) || sharedKeys.Contains(key),
            $"Unresolved resource key: {key}"));
        Assert.Contains(document.Descendants(Presentation + "MultiBinding"), binding =>
            (string?)binding.Attribute("Converter") == "{StaticResource EquipmentAktivitaetConverter}");
    }

    private static XDocument Load(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        Assert.NotNull(stream);
        return XDocument.Load(stream!);
    }

    private static HashSet<string> Keys(XDocument document) =>
        document.Root!.DescendantsAndSelf().Attributes(Xaml + "Key").Select(attribute => attribute.Value).ToHashSet(StringComparer.Ordinal);
}
