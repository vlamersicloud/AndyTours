using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

/// <summary>Begrenzte statische XAML-Verträge; keine Runtime- oder visuelle Abnahme.</summary>
public sealed class TourenEditorenOberflaechenVertragTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string[] SharedKeys = ["ApiFlaeche", "ApiKarte", "ApiKante", "ApiDialogTitel", "ApiAbschnittstitel", "ApiErklaerung", "ApiStatus", "ApiKartenrahmen", "ApiAktion", "ApiPrimaeraktion"];
    private static string Read(string name) { using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("AndyTours." + (name.EndsWith(".cs", StringComparison.Ordinal) || name.EndsWith(".xaml", StringComparison.Ordinal) ? name : name + ".xaml")); Assert.NotNull(s); using var r = new StreamReader(s!); return r.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal); }

    [Theory]
    [InlineData("EinheitBearbeitenFenster", "SpeichernCommand", "_Speichern")]
    [InlineData("EquipmentBearbeitenFenster", "OkCommand", "_OK")]
    [InlineData("WartungBearbeitenFenster", "SpeichernCommand", "OK")]
    [InlineData("AlarmBearbeitenFenster", "SpeichernCommand", "OK")]
    [InlineData("SaisonzieleFenster", "OkCommand", "_OK")]
    [InlineData("PersoenlicheDatenFenster", "OkCommand", "_OK")]
    public void FormActionsHaveFixedReachableFooter(string view, string command, string label)
    {
        var d = XDocument.Parse(Read(view));
        var button = Assert.Single(d.Descendants(P + "Button"), b => (string?)b.Attribute("Command") == "{Binding " + command + "}");
        Assert.Equal(label, (string?)button.Attribute("Content"));
        Assert.Equal("True", (string?)button.Attribute("IsDefault"));
        Assert.Empty(button.Ancestors(P + "ScrollViewer"));
        var cancel = Assert.Single(d.Root!.DescendantsAndSelf(P + "Button"), b => (string?)b.Attribute("IsCancel") == "True");
        Assert.Contains("Abbrechen", (string?)cancel.Attribute("Content"));
        Assert.Empty(cancel.Ancestors(P + "ScrollViewer"));
        if (view == "EinheitBearbeitenFenster")
            Assert.Contains(d.Descendants(P + "DockPanel"), panel => panel.Elements(P + "DockPanel.Style").Descendants(P + "DataTrigger").Any(t => (string?)t.Attribute("Binding") == "{Binding Speichert}" && (string?)t.Attribute("Value") == "True" && t.Elements(P + "Setter").Any(x => (string?)x.Attribute("Property") == "IsEnabled" && (string?)x.Attribute("Value") == "False")));
        else
            Assert.Contains(cancel.Descendants(P + "DataTrigger"), t => (string?)t.Attribute("Binding") == "{Binding Speichert}" && (string?)t.Attribute("Value") == "True" && t.Elements(P + "Setter").Any(x => (string?)x.Attribute("Property") == "IsEnabled" && (string?)x.Attribute("Value") == "False"));
        Assert.Equal("{StaticResource ApiFlaeche}", (string?)d.Root.Attribute("Background"));
    }

    [Theory]
    [InlineData("EinheitBearbeitenFenster", "DatumFeld")]
    [InlineData("EquipmentBearbeitenFenster", "NameFeld")]
    [InlineData("WartungBearbeitenFenster", "DatumFeld")]
    [InlineData("AlarmBearbeitenFenster", "RadFeld")]
    [InlineData("SaisonzieleFenster", "Tabelle")]
    [InlineData("PersoenlicheDatenFenster", "VornameFeld")]
    public void VariantControlsRemainPresent(string view, string name)
    {
        var d = XDocument.Parse(Read(view));
        Assert.Contains(d.Root!.DescendantsAndSelf().Attributes(X + "Name"), a => a.Value == name);
    }

    [Fact]
    public void TourMapAndPlotBindingsRemainNamed()
    {
        var detail = XDocument.Parse(Read("TourdetailFenster"));
        Assert.Contains(detail.Descendants(XNamespace.Get("http://oxyplot.org/wpf") + "PlotView"), e => (string?)e.Attribute("Model") == "{Binding Diagramm}" && (string?)e.Attribute("Controller") == "{Binding Steuerung}");
        Assert.Contains(detail.Descendants().Where(e => e.Name.LocalName == "MapControl"), e => (string?)e.Attribute(X + "Name") == "Karte");
        var plan = XDocument.Parse(Read("TourenplanungFenster"));
        Assert.Contains(plan.Descendants().Where(e => e.Name.LocalName == "MapControl"), e => (string?)e.Attribute(X + "Name") == "Karte");
        Assert.Contains(plan.Descendants(P + "ComboBox"), e => (string?)e.Attribute(X + "Name") == "ProfilAuswahl" && (string?)e.Attribute("SelectedValue") == "{Binding Profil}");
    }

    [Fact]
    public void SharedResourcesAreDeclaredAndAppMerged()
    {
        var app = XDocument.Parse(Read("App"));
        Assert.Contains(app.Descendants(P + "ResourceDictionary"), e => (string?)e.Attribute("Source") == "Ressourcen/ApiOberflaeche.xaml");
        var shared = XDocument.Parse(Read("ApiOberflaeche"));
        foreach (var key in SharedKeys)
            Assert.Contains(shared.Descendants().Attributes(X + "Key"), a => a.Value == key);
    }
}
