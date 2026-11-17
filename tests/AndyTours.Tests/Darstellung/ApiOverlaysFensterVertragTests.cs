using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

/// <summary>Schützt Overlay-Baumzustände, Importwarteschlange und private Pfadansicht.</summary>
public sealed class ApiOverlaysFensterVertragTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly string[] Commands =
    [
        "VerschiebenCommand", "LoeschenCommand", "NameUebernehmenCommand",
        "GpxHinzufuegenCommand", "GpxImportierenCommand", "GpxAuswahlVerwerfenCommand"
    ];

    [Fact]
    public void Xaml_keeps_tree_state_queue_privacy_commands_and_bounded_scroll_layout()
    {
        var document = Read("AndyTours.ApiOverlaysFenster.xaml");
        var tree = Assert.Single(document.Descendants(P + "TreeView"));
        Assert.Equal("{Binding IstBereit}", (string?)tree.Attribute("IsEnabled"));
        Assert.Contains(tree.Descendants(P + "Setter"), element =>
            (string?)element.Attribute("Property") == "IsSelected" &&
            ((string?)element.Attribute("Value"))?.Contains("IstAusgewaehlt, Mode=TwoWay", StringComparison.Ordinal) == true);
        Assert.Contains(tree.Descendants(P + "Setter"), element =>
            (string?)element.Attribute("Property") == "IsExpanded" &&
            ((string?)element.Attribute("Value"))?.Contains("IstAufgeklappt, Mode=TwoWay", StringComparison.Ordinal) == true);
        var grid = Assert.Single(document.Descendants(P + "DataGrid"));
        Assert.Equal("{Binding Importdateien}", (string?)grid.Attribute("ItemsSource"));
        Assert.Equal("True", (string?)grid.Attribute("IsReadOnly"));
        Assert.Equal("120", (string?)grid.Attribute("Height"));
        Assert.Equal("True", (string?)grid.Attribute("EnableRowVirtualization"));
        Assert.Contains(grid.Descendants(P + "DataGridTextColumn"), column =>
            ((string?)column.Attribute("Binding"))?.Contains("Dateiname, Mode=OneWay", StringComparison.Ordinal) == true);
        Assert.Contains(grid.Descendants(P + "DataGridTextColumn"), column =>
            ((string?)column.Attribute("Binding"))?.Contains("Status, Mode=OneWay", StringComparison.Ordinal) == true);
        Assert.DoesNotContain("Pfad", document.ToString(), StringComparison.Ordinal);
        Assert.Empty(tree.Ancestors(P + "ScrollViewer"));
        Assert.Empty(grid.Ancestors(P + "ScrollViewer"));
        Assert.Contains(document.Descendants(P + "Expander"), expander =>
            (string?)expander.Attribute("IsExpanded") == "False" &&
            expander.Descendants(P + "WrapPanel").Any(panel => panel.Descendants(P + "Button").Any()));
        Assert.Contains(document.Descendants(P + "Button"), button =>
            (string?)button.Attribute("Content") == "Schließen" && (string?)button.Attribute("IsCancel") == "True");
        Assert.Contains(document.Descendants(P + "ComboBox"), combo =>
            ((string?)combo.Attribute("ItemsSource"))?.Contains("GemeinsameFarbauswahl", StringComparison.Ordinal) == true ||
            combo.Descendants(P + "Binding").Any(binding =>
                (string?)binding.Attribute("Path") == "GemeinsameFarbauswahl"));
        foreach (var command in Commands)
            Assert.Contains(document.Descendants(P + "Button"), button =>
                (string?)button.Attribute("Command") == "{Binding " + command + "}");
    }

    private static XDocument Read(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        Assert.NotNull(stream);
        return XDocument.Load(stream!);
    }
}
