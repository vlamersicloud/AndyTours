using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

public sealed class CourseUebertragenFensterVertragTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static XDocument LadeXaml()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("AndyTours.CourseUebertragenFenster.xaml");
        Assert.NotNull(stream);
        return XDocument.Load(stream!);
    }

    [Fact]
    public void AuswahlUndNamensfelderSindWaehrenTransferGesperrt()
    {
        var document = LadeXaml();
        foreach (var name in new[] { "ProfilAuswahl", "GeraeteAuswahl" })
        {
            var control = Assert.Single(document.Descendants(Presentation + "ComboBox"),
                element => (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == name);
            Assert.Equal("{Binding IstBereit}", (string?)control.Attribute("IsEnabled"));
        }

        var names = Assert.Single(document.Descendants(Presentation + "ItemsControl"));
        Assert.Equal("{Binding IstBereit}", (string?)names.Attribute("IsEnabled"));
        Assert.Contains(document.Descendants(Presentation + "Button"),
            button => (string?)button.Attribute("Command") == "{Binding UebertragenCommand}");
        Assert.Contains(document.Descendants(Presentation + "Button"),
            button => (string?)button.Attribute("IsCancel") == "True");
    }
}