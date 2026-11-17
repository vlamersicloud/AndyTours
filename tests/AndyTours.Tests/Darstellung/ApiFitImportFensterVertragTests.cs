using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

/// <summary>Hält UI-Verträge für Aktionen und Layout des Importdialogs fest.</summary>
public sealed class ApiFitImportFensterVertragTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string[] Commands = ["HochladenCommand", "PruefenCommand", "LadenCommand", "GeraeteLesenCommand"];
    private static readonly string[] ResourceFiles = ["AndyTours.App.xaml", "AndyTours.ApiOberflaeche.xaml"];

    private static XDocument LadeXaml()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AndyTours.ApiFitImportFenster.xaml");
        Assert.NotNull(stream);
        return XDocument.Load(stream!);
    }

    /// <summary>Prüft Warteschlangenaktionen und Ressourcen einschließlich der zusammengeführten App-Dictionaries.</summary>
    [Fact]
    public void QueueCommandsAndMultiFilePickerArePresent()
    {
        var document = LadeXaml();
        var commands = document.Descendants(Presentation + "Button")
            .Select(x => (string?)x.Attribute("Command")).Where(x => x is not null).ToArray();
        foreach (var command in Commands)
            Assert.Single(commands, x => x == "{Binding " + command + "}");

        Assert.Single(document.Descendants(Presentation + "Button"), x =>
            (string?)x.Attribute("Click") == "DateienAuswaehlen");
        Assert.Contains(document.Descendants(Presentation + "Button"), button =>
            (string?)button.Attribute("Command") == "{Binding GeraeteLesenCommand}" &&
            (string?)button.Attribute("Content") == "Geräte-Dateien laden");
    }

    [Fact]
    public void QueueGridIsReadOnlyVirtualizedAndDoesNotExposeLocalPaths()
    {
        var document = LadeXaml();
        var grid = Assert.Single(document.Descendants(Presentation + "DataGrid"));
        Assert.Equal("True", (string?)grid.Attribute("IsReadOnly"));
        Assert.Equal("True", (string?)grid.Attribute("EnableRowVirtualization"));
        Assert.Equal("True", (string?)grid.Attribute("VirtualizingPanel.IsVirtualizing"));
        var columns = grid.Element(Presentation + "DataGrid.Columns")!.Elements().ToArray();
        Assert.Contains(columns, x => (string?)x.Attribute("Header") == "Datei" && ((string?)x.Attribute("Binding"))!.Contains("Dateiname"));
        Assert.Contains(columns, x => (string?)x.Attribute("Header") == "Status" && ((string?)x.Attribute("Binding"))!.Contains("Status"));
        var uploaded = Assert.Single(columns, x => (string?)x.Attribute("Header") == "Hochgeladen");
        Assert.Contains("Mode=OneWay", (string)uploaded.Attribute("Binding")!);
        Assert.DoesNotContain(document.Descendants().Attributes(), x => x.Value.Contains("LokalerPfad", StringComparison.OrdinalIgnoreCase) || x.Value.Contains("Pfad", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ContainerNodesHaveNoTextAndStaticResourcesResolveAcrossAppDictionaries()
    {
        var document = LadeXaml();
        var containers = new[] { "Grid", "StackPanel", "DockPanel", "WrapPanel" };
        Assert.DoesNotContain(document.Descendants().Where(x => containers.Contains(x.Name.LocalName)),
            x => x.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value)));

        var keys = document.Descendants().Attributes().Where(a => a.Name.LocalName == "Key")
            .Select(a => a.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var file in ResourceFiles)
        {
            var resource = XDocument.Parse(ReadResource(file));
            keys.UnionWith(resource.Descendants().Attributes(Xaml + "Key").Select(attribute => attribute.Value));
        }

        var references = document.Descendants().Attributes().Select(a => a.Value)
            .Where(v => v.Contains("{StaticResource ", StringComparison.Ordinal))
            .SelectMany(v => v.Split("{StaticResource ", StringSplitOptions.None).Skip(1))
            .Select(v => v.Split('}')[0]).ToHashSet(StringComparer.Ordinal);
        Assert.All(references, key => Assert.Contains(key, keys));
    }

    private static string ReadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}
