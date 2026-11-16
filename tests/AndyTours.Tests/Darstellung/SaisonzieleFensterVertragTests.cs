using System.Reflection;
using System.Xml.Linq;
namespace AndyTours.Tests.Darstellung;
public sealed class SaisonzieleFensterVertragTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static XDocument Load() { using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AndyTours.SaisonzieleFenster.xaml"); Assert.NotNull(stream); return XDocument.Load(stream!); }
    [Fact] public void YearAndBusyContractsRemain()
    {
        var doc = Load(); var year = Assert.Single(doc.Descendants(P + "ComboBox"), e => (string?)e.Attribute(X + "Name") == "JahrFeld");
        Assert.Equal("{Binding Jahre}", (string?)year.Attribute("ItemsSource")); Assert.Equal("{Binding GewaehltesJahr}", (string?)year.Attribute("SelectedItem")); Assert.Equal("{Binding IstGueltig}", (string?)year.Attribute("IsEnabled"));
        var grid = Assert.Single(doc.Descendants(P + "DataGrid"), e => (string?)e.Attribute(X + "Name") == "Tabelle");
        Assert.Contains(grid.Descendants(P + "DataTrigger"), t => (string?)t.Attribute("Binding") == "{Binding Speichert}" && t.Elements(P + "Setter").Any(v => (string?)v.Attribute("Property") == "IsEnabled" && (string?)v.Attribute("Value") == "False"));
    }
    [Fact] public void ContainersAndMenusHaveNoStrayTextNodes()
    {
        var doc = Load(); var names = new[] { "Grid", "StackPanel", "DockPanel", "Menu", "MenuItem" };
        Assert.DoesNotContain(doc.Descendants().Where(e => names.Contains(e.Name.LocalName)), e => e.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value)));
    }
}
