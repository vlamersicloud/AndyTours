using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiTourenMehrfachFensterVertragTests
{
    [Fact]
    public void Bulk_action_window_xaml_keeps_explicit_safe_controls_and_readonly_layout()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("AndyTours.ApiTourenMehrfachFenster.xaml");
        Assert.NotNull(stream);
        var document = XDocument.Load(stream!);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var root = document.Root!;
        Assert.Equal("AndyTours.App.Views.ApiTourenMehrfachFenster", (string?)root.Attribute(x + "Class"));

        var execute = document.Descendants(p + "Button").Single(e => (string?)e.Attribute("Content") == "Ausführen");
        Assert.Equal("{Binding AusfuehrenCommand}", (string?)execute.Attribute("Command"));
        Assert.NotEqual("True", (string?)execute.Attribute("IsDefault"));

        Assert.Contains("Mode=OneWay", (string?)root.Attribute("Title"));
        Assert.Contains(document.Descendants(p + "TextBlock"), e => ((string?)e.Attribute("Text"))?.Contains("Anzahl", StringComparison.Ordinal) == true && ((string?)e.Attribute("Text"))?.Contains("Mode=OneWay", StringComparison.Ordinal) == true);
        Assert.Contains(document.Descendants(p + "TextBlock"), e => ((string?)e.Attribute("Text"))?.Contains("Beschreibung", StringComparison.Ordinal) == true && ((string?)e.Attribute("Text"))?.Contains("Mode=OneWay", StringComparison.Ordinal) == true);
        Assert.Contains(document.Descendants(p + "Button"), e => (string?)e.Attribute("Content") == "Abbrechen" && ((string?)e.Attribute("Command"))?.Contains("AbbrechenCommand", StringComparison.Ordinal) == true);
        Assert.Contains(document.Descendants(p + "TextBlock"), e => ((string?)e.Attribute("Text"))?.Contains("Beschreibung", StringComparison.Ordinal) == true && (string?)e.Attribute("TextWrapping") == "Wrap");
        Assert.Contains(document.Descendants(p + "ScrollViewer"), _ => true);
        Assert.Contains(document.Descendants(p + "WrapPanel"), _ => true);
        Assert.Contains(document.Descendants(p + "BooleanToVisibilityConverter"), e => (string?)e.Attribute(x + "Key") == "BoolToVisibility");
        Assert.Contains(document.Descendants(p + "ProgressBar"), e => ((string?)e.Attribute("Visibility"))?.Contains("BoolToVisibility", StringComparison.Ordinal) == true);
        Assert.Contains(document.Descendants(p + "Button"), e => (string?)e.Attribute("IsCancel") == "True" && (string?)e.Attribute("Content") == "Schließen");
    }
}
