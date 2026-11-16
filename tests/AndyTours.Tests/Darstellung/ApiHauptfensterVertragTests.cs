using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

/// <summary>Strukturelle Regressionstests für die API-Hauptfenster-XAML-Datei.</summary>
public sealed class ApiHauptfensterVertragTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static XDocument LadeXaml() { using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AndyTours.ApiHauptfenster.xaml"); Assert.NotNull(stream); return XDocument.Load(stream!); }
    [Fact] public void WindowRessourcenDefinierenAlleVerwendetenStaticResources()
    {
        var document=LadeXaml(); var keys=document.Descendants(Presentation+"Window.Resources").Elements().Select(e=>(string)e.Attribute(Xaml+"Key")!).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("DauerConverter",keys); Assert.Contains("BoolToVisibilityConverter",keys);
        var refs=document.Descendants().Attributes().Select(a=>a.Value).Where(v=>v.Contains("{StaticResource ",StringComparison.Ordinal)).SelectMany(v=>v.Split("{StaticResource ",StringSplitOptions.None).Skip(1)).Select(v=>v.Split('}')[0]).ToHashSet(StringComparer.Ordinal);
        Assert.All(keys,k=>Assert.Contains(k,refs)); Assert.All(refs,k=>Assert.Contains(k,keys));
    }
    [Fact] public void TourenCrudUndTastaturbindungenBleibenVorhanden()
    {
        var d=LadeXaml(); var buttons=d.Descendants(Presentation+"Button").Select(x=>(string?)x.Attribute("Command")).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("{Binding NeueTourCommand}",buttons); Assert.Contains("{Binding TourBearbeitenCommand}",buttons); Assert.Contains("{Binding TourKopierenCommand}",buttons); Assert.Contains("{Binding TourLoeschenCommand}",buttons);
        var grid=Assert.Single(d.Descendants(Presentation+"DataGrid"),x=>(string?)x.Attribute(Xaml+"Name")=="TourenDataGrid"); Assert.Equal("Extended",(string?)grid.Attribute("SelectionMode")); Assert.Equal("{Binding AusgewaehlteTour, Mode=TwoWay}",(string?)grid.Attribute("SelectedItem")); Assert.Equal("TourenDataGrid_MouseDoubleClick",(string?)grid.Attribute("MouseDoubleClick"));
        var tourdetails=Assert.Single(d.Descendants(Presentation+"Button"),x=>(string?)x.Attribute("Content")=="Tourdetails…"); Assert.Equal("TourdetailOeffnen_Click",(string?)tourdetails.Attribute("Click"));
        var keys=grid.Element(Presentation+"DataGrid.InputBindings")!.Elements(Presentation+"KeyBinding").ToDictionary(x=>(string)x.Attribute("Key")!);
        Assert.Equal("{Binding TourBearbeitenCommand}",(string)keys["F2"].Attribute("Command")!); Assert.Equal("{Binding TourBearbeitenCommand}",(string)keys["Enter"].Attribute("Command")!); Assert.Equal("{Binding TourLoeschenCommand}",(string)keys["Delete"].Attribute("Command")!);
    }
    [Fact] public void PulsbereicheTabUndMesswerttabelleSindGebundenUndSchreibgeschuetzt()
    {
        var d=LadeXaml();
        var tab=Assert.Single(d.Descendants(Presentation+"TabItem"),x=>(string?)x.Attribute("Header")=="Pulsbereiche");
        var values=tab.Descendants().Attributes().Select(a=>a.Value).ToHashSet(StringComparer.Ordinal);
        foreach(var item in new[]{"PulsreferenzLadenCommand","GarminZonenAktualisierenCommand","PulsdatenLadenCommand","Pulsprofile","Pulsprofil","BpmBereiche","PulsReferenzText","PulsStatus","PulsDiagramm","Pulszeilen"})
            Assert.Contains(values, value => value.StartsWith("{Binding "+item+",", StringComparison.Ordinal) || value == "{Binding "+item+"}");
        var grid=Assert.Single(tab.Descendants(Presentation+"DataGrid"));
        Assert.Equal("True",(string?)grid.Attribute("IsReadOnly"));
        Assert.Equal("True",(string?)grid.Attribute("EnableRowVirtualization"));
        Assert.Equal("True",(string?)grid.Attribute("EnableColumnVirtualization"));
        Assert.Equal("False",(string?)grid.Attribute("CanUserAddRows"));
        Assert.Equal("{Binding Pulszeilen, Mode=OneWay}",(string?)grid.Attribute("ItemsSource"));
        Assert.Collection(grid.Elements(Presentation+"DataGrid.Columns").Elements(Presentation+"DataGridTextColumn"), column => Assert.Equal("Zeitraum", (string?)column.Attribute("Header")), column => Assert.Equal("Zone", (string?)column.Attribute("Header")), column => Assert.Equal("Pulsbereich", (string?)column.Attribute("Header")), column => Assert.Equal("Messzeit", (string?)column.Attribute("Header")), column => Assert.Equal("Anteil", (string?)column.Attribute("Header")));
        Assert.All(grid.Elements(Presentation+"DataGrid.Columns").Elements(Presentation+"DataGridTextColumn"),column=>Assert.Contains("Mode=OneWay",(string?)column.Attribute("Binding")));
    }
    [Fact] public void FilterDiagrammUndKpisBleibenGebunden()
    {
        var values=LadeXaml().Descendants().Attributes().Select(a=>a.Value).ToHashSet(StringComparer.Ordinal);
        foreach(var item in new[]{"AktivitaetsOptionen","KategorieOptionen","Diagramm","DistanzKmText","HoehenmeterText","FahrzeitText","AnzahlText"}) Assert.Contains("{Binding "+item+"}",values);
    }
    [Fact] public void RekordUndJahreszielListenSindGebunden()
    {
        var d=LadeXaml(); var bindings=d.Descendants(Presentation+"ListView").Select(x=>(string?)x.Attribute("ItemsSource")).ToArray();
        Assert.Contains("{Binding Jahresrekorde}",bindings); Assert.Contains("{Binding Einzelrekorde}",bindings); Assert.Contains("{Binding Jahresziele}",bindings);
    }
    [Fact] public void StammdatenMenuHandlerEindeutigUndContainerOhneFreitext()
    {
        var d=LadeXaml(); var menu=Assert.Single(d.Descendants(Presentation+"MenuItem"),x=>(string?)x.Attribute("Header")=="Stammdaten");
        var handlers=menu.Elements(Presentation+"MenuItem").Select(x=>(string?)x.Attribute("Click")).Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray();
        Assert.Equal(handlers.Length,handlers.Distinct(StringComparer.Ordinal).Count());
        var containers=new[]{"Grid","StackPanel","DockPanel","Menu","MenuItem"};
        Assert.DoesNotContain(d.Descendants().Where(x=>containers.Contains(x.Name.LocalName)),x=>x.Nodes().OfType<XText>().Any(t=>!string.IsNullOrWhiteSpace(t.Value)));
    }
    [Fact] public void DateiMenuEnthaeltFITUndGarminGeraeteImportEindeutig()
    {
        var d = LadeXaml();
        var datei = Assert.Single(d.Descendants(Presentation + "MenuItem"), x => (string?)x.Attribute("Header") == "Datei");
        Assert.Single(datei.Descendants(Presentation + "MenuItem"), x => (string?)x.Attribute("Click") == "FitDateienImportieren_Click");
        Assert.Single(datei.Descendants(Presentation + "MenuItem"), x => (string?)x.Attribute("Click") == "GarminGeraetImportieren_Click");
        var handlers = datei.Descendants(Presentation + "MenuItem").Select(x => (string?)x.Attribute("Click"))
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        Assert.Equal(handlers.Length, handlers.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact] public void GarminImportMenuIstEindeutigUndVerweistAufHandler()
    {
        var d=LadeXaml();
        var garmin=Assert.Single(d.Descendants(Presentation+"MenuItem"),x=>(string?)x.Attribute("Header")=="Garmin Connect");
        Assert.Single(garmin.Elements(Presentation+"MenuItem"),x=>(string?)x.Attribute("Header")=="Downloadaufträge…");
        Assert.Single(garmin.Elements(Presentation+"MenuItem"),x=>(string?)x.Attribute("Header")=="Garmin Connect – Einstellungen…");
    }

}
