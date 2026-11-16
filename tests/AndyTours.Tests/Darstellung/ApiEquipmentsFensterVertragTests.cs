using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

/// <summary>Strukturelle Regressionen für Equipmentfenster-Ressourcen und Aktivitätsnamenbindung.</summary>
public sealed class ApiEquipmentsFensterVertragTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void JedeStaticResourceIstImFensterDefiniertUndAktivitaetsConverterIstGebunden()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AndyTours.ApiEquipmentsFenster.xaml");
        Assert.NotNull(stream);
        var document = XDocument.Load(stream!);
        var keys = document.Descendants(Presentation + "Window.Resources").Elements()
            .Select(element => (string?)element.Attribute(Xaml + "Key")).Where(key => key is not null).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("EquipmentAktivitaetConverter", keys);
        var refs = document.Descendants().Attributes().Select(a => a.Value)
            .Where(v => v.Contains("{StaticResource ", StringComparison.Ordinal))
            .SelectMany(v => v.Split("{StaticResource ", StringSplitOptions.None).Skip(1))
            .Select(v => v.Split('}')[0]).ToHashSet(StringComparer.Ordinal);
        Assert.All(refs, key => Assert.Contains(key, keys));
        Assert.Contains(document.Descendants(Presentation + "MultiBinding"), binding =>
            (string?)binding.Attribute("Converter") == "{StaticResource EquipmentAktivitaetConverter}");
    }
}
