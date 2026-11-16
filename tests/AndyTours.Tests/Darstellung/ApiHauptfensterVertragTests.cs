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
        var grid=Assert.Single(d.Descendants(Presentation+"DataGrid")); Assert.Equal("{Binding AusgewaehlteTour, Mode=TwoWay}",(string?)grid.Attribute("SelectedItem")); Assert.Equal("TourenDataGrid_MouseDoubleClick",(string?)grid.Attribute("MouseDoubleClick"));
        var keys=grid.Element(Presentation+"DataGrid.InputBindings")!.Elements(Presentation+"KeyBinding").ToDictionary(x=>(string)x.Attribute("Key")!);
        Assert.Equal("{Binding TourBearbeitenCommand}",(string)keys["F2"].Attribute("Command")!); Assert.Equal("{Binding TourBearbeitenCommand}",(string)keys["Enter"].Attribute("Command")!); Assert.Equal("{Binding TourLoeschenCommand}",(string)keys["Delete"].Attribute("Command")!);
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
}
