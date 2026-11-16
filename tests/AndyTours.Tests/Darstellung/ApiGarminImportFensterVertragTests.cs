using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiGarminImportFensterVertragTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static XDocument Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AndyTours.ApiGarminImportFenster.xaml");
        Assert.NotNull(stream);
        return XDocument.Load(stream!);
    }

    [Fact]
    public void DefinesResourcesAndAllJobCommands()
    {
        var document = Load();
        var window = document.Root!;
        var resourceKeys = window.Element(Presentation + "Window.Resources")!.Elements()
            .Select(element => (string?)element.Attribute(XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml") + "Key"))
            .Where(key => key is not null).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("VisibleWhenTrue", resourceKeys);
        Assert.Contains("CandidateStatusConverter", resourceKeys);
        Assert.Contains("DurationConverter", resourceKeys);
        var commands = document.Descendants(Presentation + "Button").Select(button => (string?)button.Attribute("Command")).ToArray();
        foreach (var name in new[] { "StartenCommand", "LadenAktuellenCommand", "FortsetzenCommand", "AbbrechenCommand", "VorigeSeiteCommand", "NaechsteSeiteCommand" })
            Assert.Contains("{Binding " + name + "}", commands);
        Assert.Single(document.Descendants(Presentation + "WrapPanel"));
        Assert.Contains(document.Descendants(Presentation + "TextBlock"), element => ((string?)element.Attribute("Text"))?.Contains("Prüfung und Übernahme", StringComparison.Ordinal) == true);
    }
}
