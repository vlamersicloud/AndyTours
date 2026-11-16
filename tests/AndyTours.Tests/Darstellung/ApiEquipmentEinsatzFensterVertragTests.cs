using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiEquipmentEinsatzFensterVertragTests
{
    private static readonly string[] ExpectedColumns = ["Name", "VorherKm", "GesamtKm", "GesamtHm"];
    [Fact]
    public void Equipment_usage_window_exposes_readonly_columns_summary_empty_state_and_owned_close()
    {
        var root = Read("AndyTours.ApiEquipmentEinsatzFenster.xaml");
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var grid = root.Descendants(p + "DataGrid").Single();
        var columns = grid.Element(p + "DataGrid.Columns")!.Elements().ToArray();
        Assert.Equal(4, columns.Length);
        Assert.Equal("True", (string?)grid.Attribute("IsReadOnly"));
        Assert.Equal("False", (string?)grid.Attribute("CanUserAddRows"));
        Assert.Equal("False", (string?)grid.Attribute("CanUserDeleteRows"));
        Assert.Equal("EinsatzDataGrid_Sorting", (string?)grid.Attribute("Sorting"));
        Assert.Equal(ExpectedColumns, columns.Select(c => (string?)c.Attribute("SortMemberPath")));
        Assert.All(columns, c => Assert.Contains("Mode=OneWay", (string?)c.Attribute("Binding") ?? string.Join(" ", c.Descendants(p + "Binding").Select(b => (string?)b.Attribute("Mode")))));
        Assert.All(columns.Skip(1), c => Assert.Contains("N0", (string?)c.Attribute("Binding")));
        Assert.Equal("True", (string?)grid.Attribute("CanUserSortColumns"));
        Assert.DoesNotContain(columns, c => c.Attribute("SortDirection") is not null);
        Assert.Contains(root.Descendants(p + "DataTrigger"), t => (string?)t.Attribute("Binding") == "{Binding IstSumme}" &&
            (string?)t.Attribute("Value") == "True" && t.Elements(p + "Setter").Any(s => (string?)s.Attribute("Property") == "FontWeight" && (string?)s.Attribute("Value") == "Bold"));
        Assert.Contains(root.Descendants(p + "DataTrigger"), t => (string?)t.Attribute("Binding") == "{Binding HatZeilen}" && (string?)t.Attribute("Value") == "False");
        Assert.Contains(root.Descendants(p + "TextBlock"), t => (string?)t.Attribute("Text") == "Alle Touren – unabhängig von den Filtern der Übersicht.");
        Assert.Equal("False", (string?)root.Attribute("ShowInTaskbar"));
        Assert.Contains(root.Descendants(p + "Button"), b => (string?)b.Attribute("IsCancel") == "True" && (string?)b.Attribute("Content") == "Schließen");
        Assert.Contains(root.Descendants(p + "BooleanToVisibilityConverter"), c => (string?)c.Attribute(x + "Key") == "BoolToVisibility");
        Assert.Contains(root.Descendants(p + "Style"), style => (string?)style.Attribute(x + "Key") == "NumberCell");
        var resources = root.Descendants(p + "ResourceDictionary").SelectMany(d => d.Elements()).Concat(root.Element(p + "Window.Resources")!.Elements()).ToArray();
        var keys = resources.Select(e => (string?)e.Attribute(x + "Key")).Where(k => k is not null).ToHashSet();
        foreach (var reference in root.DescendantsAndSelf().Attributes().Select(a => a.Value)
                     .SelectMany(value => System.Text.RegularExpressions.Regex.Matches(value, @"\{StaticResource\s+([^}]+)\}").Select(m => m.Groups[1].Value)))
            Assert.Contains(reference, keys);
    }

    [Fact]
    public void Overview_places_exactly_one_equipment_usage_action_under_extras()
    {
        var root = Read("AndyTours.ApiHauptfenster.xaml");
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var matches = root.Descendants(p + "MenuItem").Where(item => (string?)item.Attribute("Header") == "Equipment-Einsatz…").ToArray();
        var item = Assert.Single(matches);
        Assert.Equal("EquipmentEinsatz_Click", (string?)item.Attribute("Click"));
        Assert.Equal("Extras", (string?)item.Parent?.Attribute("Header"));
    }

    private static XElement Read(string name)
    {
        using var stream = typeof(ApiEquipmentEinsatzFensterVertragTests).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Embedded XAML resource missing.");
        return XDocument.Load(stream).Root!;
    }
}
