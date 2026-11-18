using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiHauptfensterSortierungVertragTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string[] SortPfade =
    [
        "Anzeige.Datum", "Anzeige.Startzeit", "Anzeige.Name", "Anzeige.Aktivitaet",
        "Anzeige.Kategorien", "Anzeige.Equipment", "Anzeige.DistanzKm", "Anzeige.Hoehenmeter",
        "Anzeige.FahrzeitSekunden", "Anzeige.DurchschnittKmh", "Anzeige.MaxKmh", "Anzeige.HfDurchschnitt",
        "Anzeige.HfMax", "Anzeige.Trittfrequenz", "Anzeige.Temperatur", "Anzeige.MaxHoehe",
        "Anzeige.Trainingsbereich", "Anzeige.Kommentar"
    ];
    private static XDocument LoadXaml()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AndyTours.ApiHauptfenster.xaml");
        Assert.NotNull(stream);
        return XDocument.Load(stream!);
    }

    private static string ReadXamlText() => Read("AndyTours.ApiHauptfenster.xaml");

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
        var columns = grid.Element(P + "DataGrid.Columns")!.Elements().ToArray();
        Assert.Equal(SortPfade,
            columns.Select(x => (string?)x.Attribute("SortMemberPath")));
        Assert.Equal("Descending", (string?)columns[0].Attribute("SortDirection"));
        Assert.All(columns.Skip(1), x => Assert.Null(x.Attribute("SortDirection")));
        Assert.Equal("False", (string?)grid.Attribute("IsReadOnly"));
        Assert.Equal(12, columns.Count(c => c.Name.LocalName == "Zelltextspalte"));
        Assert.Equal(3, columns.Count(c => c.Name == P + "DataGridTemplateColumn"));
        Assert.Equal(3, columns.Count(c => (string?)c.Attribute("IsReadOnly") == "True"));
        Assert.Equal("True", (string?)grid.Attribute("EnableRowVirtualization"));
        Assert.Equal("True", (string?)grid.Attribute("EnableColumnVirtualization"));
        Assert.Equal("Auto", (string?)grid.Attribute("HorizontalScrollBarVisibility"));
        Assert.All(columns, column =>
        {
            var binding = column.Name == P + "DataGridTemplateColumn"
                ? (string?)Assert.Single(column.Element(P + "DataGridTemplateColumn.CellTemplate")!.Descendants(P + "TextBlock")).Attribute("Text")
                : (string?)column.Attribute("Binding");
            if (column.Name == P + "DataGridTemplateColumn")
            {
                var combo = Assert.Single(column.Element(P + "DataGridTemplateColumn.CellEditingTemplate")!.Descendants(P + "ComboBox"));
                Assert.Equal("False", (string?)combo.Attribute("IsEditable"));
                Assert.Null(combo.Attribute("SelectedItem"));
            }
            Assert.NotNull(binding);
            Assert.Equal(1, binding!.Split("Mode=OneWay", StringSplitOptions.None).Length - 1);
        });
        Assert.Contains("StringFormat='ddd, dd.MM.yyyy'", (string?)columns[0].Attribute("Binding"), StringComparison.Ordinal);
        Assert.Equal("{StaticResource TourBestDistance}", (string?)columns[6].Attribute("ElementStyle"));
        Assert.Equal("{StaticResource TourBestHeight}", (string?)columns[7].Attribute("ElementStyle"));
        Assert.Equal("{StaticResource TourBestDuration}", (string?)columns[8].Attribute("ElementStyle"));
        Assert.Equal("{StaticResource TourBestAverageSpeed}", (string?)columns[9].Attribute("ElementStyle"));
        Assert.Equal("{StaticResource TourBestMaxSpeed}", (string?)columns[10].Attribute("ElementStyle"));
        Assert.Contains("TextAlignment\" Value=\"Right\"", ReadXamlText(), StringComparison.Ordinal);
        foreach (var (style, flag) in new[]
        {
            ("TourBestDistance", "Anzeige.IstBestDistanz"),
            ("TourBestHeight", "Anzeige.IstBestHoehenmeter"),
            ("TourBestDuration", "Anzeige.IstBestFahrzeit"),
            ("TourBestAverageSpeed", "Anzeige.IstBestDurchschnittKmh"),
            ("TourBestMaxSpeed", "Anzeige.IstBestMaxKmh")
        })
        {
            var definition = Assert.Single(document.Descendants(P + "Style"), x => (string?)x.Attribute(X + "Key") == style);
            Assert.Contains(definition.Descendants(P + "DataTrigger"), x =>
                (string?)x.Attribute("Value") == "True" &&
                ((string?)x.Attribute("Binding"))?.Contains(flag, StringComparison.Ordinal) == true &&
                x.Elements(P + "Setter").Any(s => (string?)s.Attribute("Property") == "Foreground" && (string?)s.Attribute("Value") == "Red"));
            Assert.Contains(definition.Descendants(P + "Setter"), x => (string?)x.Attribute("Property") == "Foreground" && (string?)x.Attribute("Value") == "#FFFFC8C8");
            Assert.Contains(definition.Descendants(P + "Setter"), x => (string?)x.Attribute("Property") == "FontWeight" && (string?)x.Attribute("Value") == "SemiBold");
            var multi = Assert.Single(definition.Descendants(P + "MultiDataTrigger"));
            Assert.Contains(multi.Descendants(P + "Condition"), x =>
                ((string?)x.Attribute("Binding"))?.Contains(flag, StringComparison.Ordinal) == true &&
                (string?)x.Attribute("Value") == "True");
            Assert.Contains(multi.Descendants(P + "Condition"), x =>
                ((string?)x.Attribute("Binding"))?.Contains("AncestorType=DataGridRow", StringComparison.Ordinal) == true &&
                (string?)x.Attribute("Value") == "True");
        }
    }

    [Fact]
    public void Sort_event_is_handled_before_guards_and_uses_only_explicit_column_mappings()
    {
        var source = Read("AndyTours.ApiHauptfenster.Sortierung.cs");
        var handled = source.IndexOf("e.Handled = true;", StringComparison.Ordinal);
        var guard = source.IndexOf("if (_geschlossen", StringComparison.Ordinal);
        Assert.True(handled >= 0 && guard > handled);
        foreach (var path in SortPfade)
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
