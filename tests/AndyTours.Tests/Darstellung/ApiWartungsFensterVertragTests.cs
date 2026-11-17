using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiWartungsFensterVertragTests
{
    private static readonly string[] MaintenancePaths = ["Datum", "Rad", "KmStand", "KmSeitLetzter", "TageSeitLetzter", "Taetigkeit", "Kommentar"];
    private static readonly string[] AlarmPaths = ["Rad", "Taetigkeit", "IntervallKm", "GefahrenKm", "VerbleibendKm", "Zustand"];
    private static readonly int[] FormattedNumberColumns = [2, 3, 4];
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void Wartungs_und_Alarmtabellen_sind_lesend_sortierbar_und_lokal_vollstaendig_formatiert()
    {
        var document = Load("AndyTours.ApiWartungsFenster.xaml");
        var window = Assert.IsType<XElement>(document.Root);
        Assert.Equal("de-DE", (string?)window.Attribute("Language"));

        XElement Grid(string name) => document.Descendants(P + "DataGrid")
            .Single(element => (string?)element.Attribute(X + "Name") == name);
        string[] Paths(XElement grid) => grid.Element(P + "DataGrid.Columns")!.Elements()
            .Select(element => (string)element.Attribute("SortMemberPath")!).ToArray();

        var maintenance = Grid("WartungenGrid");
        var alarms = Grid("AlarmeGrid");
        Assert.Equal(MaintenancePaths, Paths(maintenance));
        Assert.Equal(AlarmPaths, Paths(alarms));
        var tableStyle = document.Descendants(P + "Style").Single(style => (string?)style.Attribute(X + "Key") == "Table");
        string Setter(string property) => tableStyle.Elements(P + "Setter").Single(setter => (string?)setter.Attribute("Property") == property).Attribute("Value")!.Value;
        Assert.Equal("True", Setter("IsReadOnly"));
        Assert.Equal("False", Setter("CanUserAddRows"));
        Assert.Equal("False", Setter("CanUserDeleteRows"));
        Assert.Equal("Extended", Setter("SelectionMode"));
        AssertGridBindingsAndEvents(maintenance, "Wartungen_SelectionChanged", "Wartungen_DoubleClick", "Wartungen_KeyDown");
        AssertGridBindingsAndEvents(alarms, "Alarme_SelectionChanged", "Alarme_DoubleClick", "Alarme_KeyDown");

        Assert.Equal("Auto", (string?)tableStyle.Elements(P + "Setter")
            .Single(setter => (string?)setter.Attribute("Property") == "ScrollViewer.HorizontalScrollBarVisibility").Attribute("Value"));
        Assert.Equal("True", (string?)tableStyle.Elements(P + "Setter")
            .Single(setter => (string?)setter.Attribute("Property") == "EnableColumnVirtualization").Attribute("Value"));
        Assert.Contains(document.Descendants(P + "Style").Single(style => (string?)style.Attribute(X + "Key") == "MaintenanceNumber")
            .Elements(P + "Setter"), setter => (string?)setter.Attribute("Property") == "TextAlignment" && (string?)setter.Attribute("Value") == "Right");
        Assert.Contains(document.Descendants(P + "TextBlock"), element =>
            ((string?)element.Attribute("Text"))?.Contains("Haltbarkeit:", StringComparison.Ordinal) == true);
        Assert.Contains(document.Descendants(P + "DataGridTextColumn"), element =>
            ((string?)element.Attribute("Binding"))?.Contains("StringFormat='dd.MM.yyyy'", StringComparison.Ordinal) == true);
        var maintenanceColumns = maintenance.Element(P + "DataGrid.Columns")!.Elements().ToArray();
        var alarmColumns = alarms.Element(P + "DataGrid.Columns")!.Elements().ToArray();
        foreach (var index in FormattedNumberColumns) Assert.Equal("{StaticResource MaintenanceNumber}", (string?)maintenanceColumns[index].Attribute("ElementStyle"));
        foreach (var index in FormattedNumberColumns) Assert.Equal("{StaticResource MaintenanceNumber}", (string?)alarmColumns[index].Attribute("ElementStyle"));
        Assert.Contains("StringFormat=N1", (string?)maintenanceColumns[2].Attribute("Binding"));
        Assert.Contains("StringFormat=N1", (string?)maintenanceColumns[3].Attribute("Binding"));
        Assert.Contains("StringFormat=N0", (string?)alarmColumns[2].Attribute("Binding"));
        Assert.Contains("StringFormat=N1", (string?)alarmColumns[3].Attribute("Binding"));
        Assert.Contains("StringFormat=N1", (string?)alarmColumns[4].Attribute("Binding"));
        var localKeys = document.Descendants().Attributes(X + "Key").Select(attribute => attribute.Value).ToHashSet(StringComparer.Ordinal);
        var sharedKeys = Keys(Load("AndyTours.App.xaml"));
        sharedKeys.UnionWith(Keys(Load("AndyTours.ApiOberflaeche.xaml")));
        var references = document.Descendants().Attributes()
            .Where(attribute => attribute.Name.LocalName == "Style" && attribute.Value.StartsWith("{StaticResource ", StringComparison.Ordinal))
            .Select(attribute => attribute.Value["{StaticResource ".Length..^1]);
        Assert.All(references, key => Assert.True(localKeys.Contains(key) || sharedKeys.Contains(key), $"Unresolved style: {key}"));
    }

    private static void AssertGridBindingsAndEvents(XElement grid, string selection, string doubleClick, string keyDown)
    {
        Assert.Equal("{StaticResource Table}", (string?)grid.Attribute("Style"));
        Assert.Equal(selection, (string?)grid.Attribute("SelectionChanged"));
        Assert.Equal(doubleClick, (string?)grid.Attribute("PreviewMouseDoubleClick"));
        Assert.Equal(keyDown, (string?)grid.Attribute("PreviewKeyDown"));
        foreach (var column in grid.Element(P + "DataGrid.Columns")!.Elements())
            Assert.Equal(1, ((string?)column.Attribute("Binding"))!.Split("Mode=OneWay", StringSplitOptions.None).Length - 1);
    }

    private static XDocument Load(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        Assert.NotNull(stream);
        return XDocument.Load(stream!);
    }

    private static HashSet<string> Keys(XDocument document) =>
        document.Root!.DescendantsAndSelf().Attributes(X + "Key").Select(attribute => attribute.Value).ToHashSet(StringComparer.Ordinal);
}