using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

/// <summary>Hält UI-Verträge für Aktionen und Layout des Importdialogs fest.</summary>
public sealed class ApiGarminImportFensterVertragTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string[] ExpectedCommands = ["StartenCommand", "LadenAktuellenCommand", "FortsetzenCommand", "AbbrechenCommand", "VorigeSeiteCommand", "NaechsteSeiteCommand", "PruefenCommand"];

    private static XDocument Load(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        Assert.NotNull(stream);
        return XDocument.Load(stream!);
    }

    [Fact]
    public void DefinesResourcesAndReviewCommand()
    {
        var d = Load("AndyTours.ApiGarminImportFenster.xaml");
        var keys = d.Root!.Element(P + "Window.Resources")!.Elements().Select(e => (string?)e.Attribute(X + "Key")).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("VisibleWhenTrue", keys); Assert.Contains("CandidateStatusConverter", keys); Assert.Contains("DurationConverter", keys);
        var commands = d.Descendants(P + "Button").Select(b => (string?)b.Attribute("Command")).ToArray();
        foreach (var name in ExpectedCommands) Assert.Contains("{Binding " + name + "}", commands);
        var footer = Assert.Single(d.Descendants(P + "WrapPanel"), panel =>
            panel.Ancestors(P + "ScrollViewer").Any() == false &&
            panel.Descendants(P + "Button").Any(button => (string?)button.Attribute("Command") == "{Binding StartenCommand}") &&
            panel.Descendants(P + "Button").Any(button => (string?)button.Attribute("Command") == "{Binding AbbrechenCommand}"));
        Assert.Equal("Right", (string?)footer.Attribute("HorizontalAlignment"));
        Assert.Contains(d.Descendants(P + "TextBlock"), e => ((string?)e.Attribute("Text"))?.Contains("FIT-Dateien", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void ReviewDialogLocksAndShowsHintsWhileSaving()
    {
        var d = Load("AndyTours.GarminImportFenster.xaml");
        Assert.Contains(d.Descendants(P + "DataGrid"), grid => grid.Element(P + "DataGrid.Style")?.Descendants(P + "DataTrigger").Any(t => (string?)t.Attribute("Binding") == "{Binding Speichert}" && t.Elements(P + "Setter").Any(s => (string?)s.Attribute("Property") == "IsEnabled" && (string?)s.Attribute("Value") == "False")) == true);
        var cancel = Assert.Single(d.Descendants(P + "Button"), b => (string?)b.Attribute(X + "Name") == "AbbrechenButton");
        Assert.Contains(cancel.Descendants(P + "DataTrigger"), t => (string?)t.Attribute("Binding") == "{Binding Speichert}" && t.Elements(P + "Setter").Any(s => (string?)s.Attribute("Property") == "IsEnabled" && (string?)s.Attribute("Value") == "False"));
        Assert.Contains(d.Descendants(P + "DataGridTextColumn"), c => (string?)c.Attribute("Header") == "Hinweis" && (string?)c.Attribute("Binding") == "{Binding Hinweis, Mode=OneWay}");
        Assert.Contains(d.Descendants(P + "TextBlock"), t => (string?)t.Attribute("Text") == "{Binding ErklaerungText}");
    }
}
