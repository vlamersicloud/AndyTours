using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

/// <summary>Hält Spalten-, Summen-, Leerzustands- und Sicherheitsverträge des Einsatzberichts fest.</summary>
public sealed class ApiEquipmentEinsatzFensterVertragTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string[] ExpectedColumns = ["Name", "VorherKm", "GesamtKm", "GesamtHm"];

    [Fact]
    public void Equipment_usage_window_exposes_readonly_columns_summary_empty_state_and_owned_close()
    {
        var root = Read("AndyTours.ApiEquipmentEinsatzFenster.xaml");
        var grid = root.Descendants(P + "DataGrid").Single();
        var columns = grid.Element(P + "DataGrid.Columns")!.Elements().ToArray();
        Assert.Equal(4, columns.Length);
        Assert.Equal("True", (string?)grid.Attribute("IsReadOnly"));
        Assert.Equal("False", (string?)grid.Attribute("CanUserAddRows"));
        Assert.Equal("False", (string?)grid.Attribute("CanUserDeleteRows"));
        Assert.Equal("EinsatzDataGrid_Sorting", (string?)grid.Attribute("Sorting"));
        Assert.Equal(ExpectedColumns, columns.Select(c => (string?)c.Attribute("SortMemberPath")));
        Assert.All(columns, c => Assert.Contains("Mode=OneWay", (string?)c.Attribute("Binding") ?? string.Join(" ", c.Descendants(P + "Binding").Select(b => (string?)b.Attribute("Mode")))));
        Assert.All(columns.Skip(1), c => Assert.Contains("N0", (string?)c.Attribute("Binding")));
        Assert.Equal("True", (string?)grid.Attribute("CanUserSortColumns"));
        Assert.DoesNotContain(columns, c => c.Attribute("SortDirection") is not null);
        Assert.Contains(root.Descendants(P + "DataTrigger"), t => (string?)t.Attribute("Binding") == "{Binding IstSumme}" &&
            (string?)t.Attribute("Value") == "True" && t.Elements(P + "Setter").Any(s => (string?)s.Attribute("Property") == "FontWeight" && (string?)s.Attribute("Value") == "Bold"));
        Assert.Contains(root.Descendants(P + "DataTrigger"), t => (string?)t.Attribute("Binding") == "{Binding HatZeilen}" && (string?)t.Attribute("Value") == "False");
        Assert.Contains(root.Descendants(P + "TextBlock"), t => (string?)t.Attribute("Text") == "Alle Touren – unabhängig von den Filtern der Übersicht.");
        Assert.Equal("False", (string?)root.Attribute("ShowInTaskbar"));
        Assert.Contains(root.Descendants(P + "Button"), b => (string?)b.Attribute("IsCancel") == "True" && (string?)b.Attribute("Content") == "Schließen");
        Assert.Contains(root.Descendants(P + "BooleanToVisibilityConverter"), c => (string?)c.Attribute(X + "Key") == "BoolToVisibility");
        Assert.Contains(root.Descendants(P + "Style"), style => (string?)style.Attribute(X + "Key") == "NumberCell");
        Assert.Contains(grid.Ancestors(P + "Grid"), parent => (string?)parent.Attribute("Grid.Row") == "1");
        Assert.Empty(grid.Ancestors(P + "ScrollViewer"));

        var localKeys = root.Descendants().Attributes(X + "Key").Select(attribute => attribute.Value).ToHashSet(StringComparer.Ordinal);
        var sharedKeys = Keys(Read("AndyTours.App.xaml"));
        sharedKeys.UnionWith(Keys(Read("AndyTours.ApiOberflaeche.xaml")));
        var references = root.DescendantsAndSelf().Attributes().Select(attribute => attribute.Value)
            .Where(value => value.Contains("{StaticResource ", StringComparison.Ordinal))
            .SelectMany(value => value.Split("{StaticResource ", StringSplitOptions.None).Skip(1))
            .Select(value => value.Split('}')[0]).ToHashSet(StringComparer.Ordinal);
        Assert.All(references, reference => Assert.True(localKeys.Contains(reference) || sharedKeys.Contains(reference),
            $"Unresolved resource key: {reference}"));
    }

    [Fact]
    public void Overview_places_exactly_one_equipment_usage_action_under_extras()
    {
        var root = Read("AndyTours.ApiHauptfenster.xaml");
        var matches = root.Descendants(P + "MenuItem").Where(item => (string?)item.Attribute("Header") == "Equipment-Einsatz…").ToArray();
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

    private static HashSet<string> Keys(XElement root) =>
        root.DescendantsAndSelf().Attributes(X + "Key").Select(attribute => attribute.Value).ToHashSet(StringComparer.Ordinal);
}
