using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Tabellenlayout;

public sealed class EinheitentabellenlayoutIntegrationTests
{
    private static readonly string[] Paths = ["Anzeige.Datum", "Anzeige.Startzeit", "Anzeige.Name", "Anzeige.Aktivitaet", "Anzeige.Kategorien", "Anzeige.Equipment", "Anzeige.DistanzKm", "Anzeige.Hoehenmeter", "Anzeige.FahrzeitSekunden", "Anzeige.DurchschnittKmh", "Anzeige.MaxKmh", "Anzeige.HfDurchschnitt", "Anzeige.HfMax", "Anzeige.Trittfrequenz", "Anzeige.Temperatur", "Anzeige.MaxHoehe", "Anzeige.Trainingsbereich", "Anzeige.Kommentar"];
    private static string Resource(string name) { using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AndyTours." + name); Assert.NotNull(stream); using var reader = new StreamReader(stream!); return reader.ReadToEnd(); }

    [Fact]
    public void MainGridHasExpectedPathsAndVirtualization()
    {
        var document = XDocument.Parse(Resource("ApiHauptfenster.xaml"));
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var grid = Assert.Single(document.Descendants(p + "DataGrid"), e => (string?)e.Attribute(x + "Name") == "TourenDataGrid");
        Assert.Equal("True", (string?)grid.Attribute("EnableRowVirtualization"));
        Assert.Equal("True", (string?)grid.Attribute("EnableColumnVirtualization"));
        Assert.Equal(Paths, grid.Element(p + "DataGrid.Columns")!.Elements().Select(e => (string?)e.Attribute("SortMemberPath")).ToArray());
    }

    [Fact]
    public void MainHooksCompositionAndLayoutSafetyArePresent()
    {
        var partial = Resource("ApiHauptfenster.Spaltenlayout.cs");
        foreach (var path in Paths) Assert.Contains("\"" + path + "\"", partial, StringComparison.Ordinal);
        Assert.Contains("private async Task SpaltenlayoutInitialisieren()", partial, StringComparison.Ordinal);
        Assert.Contains("InstalliereSpaltenmenue();", partial, StringComparison.Ordinal);
        Assert.Contains("Spaltenlayout neu laden", partial, StringComparison.Ordinal);
        Assert.Contains("SperreSpaltenlayout(", partial, StringComparison.Ordinal);
        Assert.Contains("Width.Value", partial, StringComparison.Ordinal);
        Assert.Contains("BeobachterEntfernen();", partial, StringComparison.Ordinal);
        Assert.Contains("private void SpaltenlayoutDispose()", partial, StringComparison.Ordinal);
        var main = Resource("ApiHauptfenster.xaml.cs");
        Assert.Contains("SpaltenlayoutLoaded", main, StringComparison.Ordinal);
        Assert.Contains("SpaltenlayoutDispose", main, StringComparison.Ordinal);
        var program = Resource("Program.cs");
        Assert.Contains("AddSingleton<IEinheitentabellenlayout", program, StringComparison.Ordinal);
        Assert.Contains("MapEinheitentabellenlayout", program, StringComparison.Ordinal);
        var session = Resource("BackendClientSitzung.cs");
        Assert.Contains("EinheitentabellenlayoutHttpClient", session, StringComparison.Ordinal);
        var app = Resource("App.Api.cs");
        Assert.Contains("sitzung.Einheitentabellenlayout", app, StringComparison.Ordinal);
        Assert.Contains("Spaltenlayout", partial, StringComparison.Ordinal);
    }
}
