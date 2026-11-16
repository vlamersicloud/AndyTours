using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiHauptfensterSortierungVertragTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string[] SortPfade = ["Werte.Datum", "Werte.Startzeit", "Werte.Name", "Werte.DistanzKm", "Werte.Hoehenmeter", "Werte.FahrzeitSekunden"] ;
    private static XDocument LoadXaml()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AndyTours.ApiHauptfenster.xaml");
        Assert.NotNull(stream);
        return XDocument.Load(stream!);
    }

    private static string Read(string resource)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Tour_grid_columns_are_wired_to_the_finite_server_sort_keys()
    {
        var document = LoadXaml();
        var grid = Assert.Single(document.Descendants(P + "DataGrid"), x => (string?)x.Attribute(X + "Name") == "TourenDataGrid");
        Assert.Equal("TourenDataGrid_Sorting", (string?)grid.Attribute("Sorting"));
        Assert.Equal("True", (string?)grid.Attribute("CanUserSortColumns"));
        var columns = grid.Element(P + "DataGrid.Columns")!.Elements(P + "DataGridTextColumn").ToArray();
        Assert.Equal(SortPfade,
            columns.Select(x => (string?)x.Attribute("SortMemberPath")));
        Assert.Equal("Descending", (string?)columns[0].Attribute("SortDirection"));
        Assert.All(columns.Skip(1), x => Assert.Null(x.Attribute("SortDirection")));
        Assert.Equal("True", (string?)grid.Attribute("IsReadOnly"));
        Assert.Equal("True", (string?)grid.Attribute("EnableRowVirtualization"));
    }

    [Fact]
    public void Sort_event_is_handled_before_guards_and_uses_only_explicit_column_mappings()
    {
        var source = Read("AndyTours.ApiHauptfenster.Sortierung.cs");
        var handled = source.IndexOf("e.Handled = true;", StringComparison.Ordinal);
        var guard = source.IndexOf("if (_geschlossen", StringComparison.Ordinal);
        Assert.True(handled >= 0 && guard > handled);
        foreach (var path in new[] { "Werte.Datum", "Werte.Startzeit", "Werte.Name", "Werte.DistanzKm", "Werte.Hoehenmeter", "Werte.FahrzeitSekunden" })
            Assert.Contains("\"" + path + "\" => TourenSortierschluessel.", source, StringComparison.Ordinal);
        Assert.Contains("var ct = _lifetime.Token;", source, StringComparison.Ordinal);
        Assert.Contains("SortiereTourenAsync(key, absteigend, ct)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SortDescription", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CollectionViewSource", source, StringComparison.Ordinal);
    }

    [Fact]
    public void View_model_carries_sort_state_through_refresh_and_paging_and_releases_busy_state()
    {
        var vm = Read("AndyTours.ApiUebersichtViewModel.cs");
        var sorting = Read("AndyTours.ApiUebersichtViewModel.Sortierung.cs");
        Assert.Equal(2, Count(vm, "Sortierung = TourenSortierung, Absteigend = TourenAbsteigend"));
        Assert.Contains("private TourenSortierschluessel _tourenSortierung = TourenSortierschluessel.Datum;", sorting, StringComparison.Ordinal);
        Assert.Contains("private bool _tourenAbsteigend = true;", sorting, StringComparison.Ordinal);
        Assert.Contains("finally", sorting, StringComparison.Ordinal);
        Assert.Contains("Laedt = false;", sorting, StringComparison.Ordinal);
        Assert.Contains("ValidiereSortierantwort(page);", sorting, StringComparison.Ordinal);
        Assert.Contains("generation == Interlocked.Read(ref _generation) && !ct.IsCancellationRequested", sorting, StringComparison.Ordinal);
    }

    private static int Count(string source, string value) => source.Split(value, StringSplitOptions.None).Length - 1;
}
