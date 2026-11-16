using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiWartungsFensterVertragTests
{
    private static readonly string[] MaintenancePaths = ["Datum", "Rad", "KmStand", "KmSeitLetzter", "TageSeitLetzter", "Taetigkeit", "Kommentar"];
    private static readonly string[] AlarmPaths = ["Rad", "Taetigkeit", "IntervallKm", "GefahrenKm", "VerbleibendKm", "Zustand"];
    [Fact]
    public void Wartungs_und_Alarmtabellen_sind_lesend_sortierbar_und_lokal_vollstaendig_formatiert()
    {
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AndyTours.ApiWartungsFenster.xaml");
        Assert.NotNull(stream);
        var document = XDocument.Load(stream!);
        var window = Assert.IsType<XElement>(document.Root);
        Assert.Equal("de-DE", (string?)window.Attribute("Language"));

        XElement Grid(string name) => document.Descendants(p + "DataGrid")
            .Single(element => (string?)element.Attribute(x + "Name") == name);
        string[] Paths(XElement grid) => grid.Element(p + "DataGrid.Columns")!.Elements()
            .Select(element => (string)element.Attribute("SortMemberPath")!).ToArray();

        var maintenance = Grid("WartungenGrid");
        var alarms = Grid("AlarmeGrid");
        Assert.Equal(MaintenancePaths, Paths(maintenance));
        Assert.Equal(AlarmPaths, Paths(alarms));
        var tableStyle = document.Descendants(p + "Style").Single(style => (string?)style.Attribute(x + "Key") == "Table");
        string Setter(string property) => tableStyle.Elements(p + "Setter").Single(setter => (string?)setter.Attribute("Property") == property).Attribute("Value")!.Value;
        Assert.Equal("True", Setter("IsReadOnly"));
        Assert.Equal("False", Setter("CanUserAddRows"));
        Assert.Equal("False", Setter("CanUserDeleteRows"));
        Assert.Equal("Extended", Setter("SelectionMode"));
        foreach (var grid in new[] { maintenance, alarms })
        {
            Assert.Equal("{StaticResource Table}", (string?)grid.Attribute("Style"));
            Assert.Contains("SelectionChanged", (string?)grid.Attribute("SelectionChanged"));
            Assert.Contains("DoubleClick", (string?)grid.Attribute("PreviewMouseDoubleClick"));
            Assert.Contains("KeyDown", (string?)grid.Attribute("PreviewKeyDown"));
            foreach (var column in grid.Element(p + "DataGrid.Columns")!.Elements())
                Assert.Equal(1, ((string?)column.Attribute("Binding"))!.Split("Mode=OneWay", StringSplitOptions.None).Length - 1);
        }

        Assert.Equal("Auto", (string?)document.Descendants(p + "Style")
            .Single(style => (string?)style.Attribute(x + "Key") == "Table")
            .Elements(p + "Setter").Single(setter => (string?)setter.Attribute("Property") == "ScrollViewer.HorizontalScrollBarVisibility").Attribute("Value"));
        Assert.Equal("True", (string?)document.Descendants(p + "Style").Single(style => (string?)style.Attribute(x + "Key") == "Table")
            .Elements(p + "Setter").Single(setter => (string?)setter.Attribute("Property") == "EnableColumnVirtualization").Attribute("Value"));
        Assert.Contains(document.Descendants(p + "Style").Single(style => (string?)style.Attribute(x + "Key") == "MaintenanceNumber")
            .Elements(p + "Setter"), setter => (string?)setter.Attribute("Property") == "TextAlignment" && (string?)setter.Attribute("Value") == "Right");
        Assert.Contains(document.Descendants(p + "TextBlock"), element =>
            ((string?)element.Attribute("Text"))?.Contains("Haltbarkeit:", StringComparison.Ordinal) == true);
        Assert.Contains(document.Descendants(p + "DataGridTextColumn"), element =>
            ((string?)element.Attribute("Binding"))?.Contains("StringFormat='dd.MM.yyyy'", StringComparison.Ordinal) == true);
        var maintenanceColumns = maintenance.Element(p + "DataGrid.Columns")!.Elements().ToArray();
        var alarmColumns = alarms.Element(p + "DataGrid.Columns")!.Elements().ToArray();
        foreach (var index in new[] { 2, 3, 4 }) Assert.Equal("{StaticResource MaintenanceNumber}", (string?)maintenanceColumns[index].Attribute("ElementStyle"));
        foreach (var index in new[] { 2, 3, 4 }) Assert.Equal("{StaticResource MaintenanceNumber}", (string?)alarmColumns[index].Attribute("ElementStyle"));
        Assert.Contains("StringFormat=N1", (string?)maintenanceColumns[2].Attribute("Binding"));
        Assert.Contains("StringFormat=N1", (string?)maintenanceColumns[3].Attribute("Binding"));
        Assert.Contains("StringFormat=N0", (string?)alarmColumns[2].Attribute("Binding"));
        Assert.Contains("StringFormat=N1", (string?)alarmColumns[3].Attribute("Binding"));
        Assert.Contains("StringFormat=N1", (string?)alarmColumns[4].Attribute("Binding"));
        var keys = document.Descendants().Attributes(x + "Key").Select(attribute => attribute.Value).ToHashSet(StringComparer.Ordinal);
        var references = document.Descendants().Attributes().Where(attribute => attribute.Name.LocalName == "Style" && attribute.Value.StartsWith("{StaticResource ", StringComparison.Ordinal))
            .Select(attribute => attribute.Value["{StaticResource ".Length..^1]);
        Assert.All(references, key => Assert.Contains(key, keys));
    }
}
