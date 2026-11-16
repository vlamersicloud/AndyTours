using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiOverlaysFensterVertragTests
{
    [Fact]
    public void Xaml_keeps_tree_state_queue_privacy_and_commands_bound()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AndyTours.ApiOverlaysFenster.xaml");
        Assert.NotNull(stream);
        var document = XDocument.Load(stream!);
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        string source = document.ToString(SaveOptions.DisableFormatting);

        var tree = Assert.Single(document.Descendants(wpf + "TreeView"));
        Assert.Equal("{Binding IstBereit}", (string?)tree.Attribute("IsEnabled"));
        Assert.Contains(tree.Descendants(wpf + "Setter"), element =>
            (string?)element.Attribute("Property") == "IsSelected" &&
            ((string?)element.Attribute("Value"))!.Contains("IstAusgewaehlt, Mode=TwoWay", StringComparison.Ordinal));
        Assert.Contains(tree.Descendants(wpf + "Setter"), element =>
            (string?)element.Attribute("Property") == "IsExpanded" &&
            ((string?)element.Attribute("Value"))!.Contains("IstAufgeklappt, Mode=TwoWay", StringComparison.Ordinal));
        var grid = Assert.Single(document.Descendants(wpf + "DataGrid"));
        Assert.Equal("True", (string?)grid.Attribute("IsReadOnly"));
        Assert.Equal("120", (string?)grid.Attribute("Height"));
        Assert.Equal("True", (string?)grid.Attribute("EnableRowVirtualization"));
        Assert.Contains(grid.Descendants(wpf + "DataGridTextColumn"), column => ((string?)column.Attribute("Binding"))?.Contains("Dateiname, Mode=OneWay", StringComparison.Ordinal) == true);
        Assert.Contains(grid.Descendants(wpf + "DataGridTextColumn"), column => ((string?)column.Attribute("Binding"))?.Contains("Status, Mode=OneWay", StringComparison.Ordinal) == true);
        Assert.DoesNotContain("Pfad", source, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", source, StringComparison.Ordinal);
        Assert.Contains("GemeinsameFarbauswahl", source, StringComparison.Ordinal);
        foreach (string command in new[] { "VerschiebenCommand", "LoeschenCommand", "NameUebernehmenCommand", "GpxHinzufuegenCommand", "GpxImportierenCommand", "GpxAuswahlVerwerfenCommand" })
            Assert.Contains(command, source, StringComparison.Ordinal);
    }
}
