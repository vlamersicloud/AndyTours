using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiFitImportFensterVertragTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static XDocument LadeXaml()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AndyTours.ApiFitImportFenster.xaml");
        Assert.NotNull(stream);
        return XDocument.Load(stream!);
    }

    [Fact]
    public void QueueCommandsAndMultiFilePickerArePresent()
    {
        var document = LadeXaml();
        var commands = document.Descendants(Presentation + "Button")
            .Select(x => (string?)x.Attribute("Command")).Where(x => x is not null).ToArray();
        foreach (var command in new[] { "HochladenCommand", "PruefenCommand", "LadenCommand", "GeraeteLesenCommand" })
            Assert.Single(commands, x => x == "{Binding " + command + "}");

        Assert.Single(document.Descendants(Presentation + "Button"), x =>
            (string?)x.Attribute("Click") == "DateienAuswaehlen");
    }

    [Fact]
    public void QueueGridIsReadOnlyVirtualizedAndDoesNotExposeLocalPaths()
    {
        var document = LadeXaml();
        var grid = Assert.Single(document.Descendants(Presentation + "DataGrid"));
        Assert.Equal("True", (string?)grid.Attribute("IsReadOnly"));
        Assert.Equal("True", (string?)grid.Attribute("EnableRowVirtualization"));
        Assert.Equal("True", (string?)grid.Attribute(Presentation + "VirtualizingPanel.IsVirtualizing"));
        var columns = grid.Element(Presentation + "DataGrid.Columns")!.Elements().ToArray();
        Assert.Contains(columns, x => (string?)x.Attribute("Header") == "Datei" && ((string?)x.Attribute("Binding"))!.Contains("Dateiname"));
        Assert.Contains(columns, x => (string?)x.Attribute("Header") == "Status" && ((string?)x.Attribute("Binding"))!.Contains("Status"));
        var uploaded = Assert.Single(columns, x => (string?)x.Attribute("Header") == "Hochgeladen");
        Assert.Contains("Mode=OneWay", (string)uploaded.Attribute("Binding")!);
        Assert.DoesNotContain(document.Descendants().Attributes(), x => x.Value.Contains("LokalerPfad", StringComparison.OrdinalIgnoreCase) || x.Value.Contains("Pfad", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ContainerNodesHaveNoTextAndThereAreNoUnresolvedStaticResources()
    {
        var document = LadeXaml();
        var containers = new[] { "Grid", "StackPanel", "DockPanel", "WrapPanel" };
        Assert.DoesNotContain(document.Descendants().Where(x => containers.Contains(x.Name.LocalName)),
            x => x.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value)));

        var keys = document.Descendants().Attributes().Where(a => a.Name.LocalName == "Key")
            .Select(a => a.Value).ToHashSet(StringComparer.Ordinal);
        var references = document.Descendants().Attributes().Select(a => a.Value)
            .Where(v => v.Contains("{StaticResource ", StringComparison.Ordinal))
            .SelectMany(v => v.Split("{StaticResource ", StringSplitOptions.None).Skip(1))
            .Select(v => v.Split('}')[0]).ToHashSet(StringComparer.Ordinal);
        Assert.All(references, key => Assert.Contains(key, keys));
    }
}
