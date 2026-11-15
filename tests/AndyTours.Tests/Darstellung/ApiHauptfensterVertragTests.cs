using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

/// <summary>Strukturelle Regressionstests für die API-Hauptfenster-XAML-Datei.</summary>
public sealed class ApiHauptfensterVertragTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument LadeXaml()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("AndyTours.ApiHauptfenster.xaml");
        Assert.NotNull(stream);
        return XDocument.Load(stream!);
    }

    [Fact]
    public void WindowRessourcenDefinierenAlleVerwendetenStaticResources()
    {
        var document = LadeXaml();
        var resourceKeys = document.Descendants(Presentation + "Window.Resources")
            .Elements()
            .Select(element => (string)element.Attribute(Xaml + "Key")!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("DauerConverter", resourceKeys);
        Assert.Contains("BoolToVisibilityConverter", resourceKeys);

        var staticReferences = document.Descendants()
            .Attributes()
            .Select(attribute => attribute.Value)
            .Where(value => value.Contains("{StaticResource ", StringComparison.Ordinal))
            .SelectMany(value => value.Split("{StaticResource ", StringSplitOptions.None).Skip(1))
            .Select(value => value.Split('}', StringSplitOptions.None)[0])
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(resourceKeys, key => Assert.Contains(key, staticReferences));
        Assert.All(staticReferences, key => Assert.Contains(key, resourceKeys));
    }

    [Fact]
    public void TourenCrudAuswahlDoppelklickUndTastaturbindungenBleibenVorhanden()
    {
        var document = LadeXaml();
        var buttons = document.Descendants(Presentation + "Button")
            .Select(button => (string?)button.Attribute("Command"))
            .Where(value => value is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("{Binding NeueTourCommand}", buttons);
        Assert.Contains("{Binding TourBearbeitenCommand}", buttons);
        Assert.Contains("{Binding TourKopierenCommand}", buttons);
        Assert.Contains("{Binding TourLoeschenCommand}", buttons);

        var grid = Assert.Single(document.Descendants(Presentation + "DataGrid"));
        Assert.Equal("{Binding AusgewaehlteTour, Mode=TwoWay}", (string?)grid.Attribute("SelectedItem"));
        Assert.Equal("TourenDataGrid_MouseDoubleClick", (string?)grid.Attribute("MouseDoubleClick"));

        var keys = grid.Element(Presentation + "DataGrid.InputBindings")!
            .Elements(Presentation + "KeyBinding")
            .ToDictionary(binding => (string)binding.Attribute("Key")!, binding => (string)binding.Attribute("Command")!);
        Assert.Equal("{Binding TourBearbeitenCommand}", keys["F2"]);
        Assert.Equal("{Binding TourBearbeitenCommand}", keys["Enter"]);
        Assert.Equal("{Binding TourLoeschenCommand}", keys["Delete"]);
    }

    [Fact]
    public void AktivitaetsKategorieDiagrammUndKpiBindingsBleibenVorhanden()
    {
        var document = LadeXaml();
        var values = document.Descendants()
            .Attributes()
            .Select(attribute => attribute.Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("{Binding AktivitaetsOptionen}", values);
        Assert.Contains("{Binding KategorieOptionen}", values);
        Assert.Contains("{Binding Diagramm}", values);
        Assert.Contains("{Binding DistanzKmText}", values);
        Assert.Contains("{Binding HoehenmeterText}", values);
        Assert.Contains("{Binding FahrzeitText}", values);
        Assert.Contains("{Binding AnzahlText}", values);
    }
}
