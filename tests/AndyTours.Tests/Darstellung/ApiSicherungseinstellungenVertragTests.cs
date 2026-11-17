namespace AndyTours.Tests.Darstellung;

using System.Xml.Linq;
using Xunit;

public sealed class ApiSicherungseinstellungenVertragTests
{
    private static XDocument Resource(string name)
    {
        using var stream = typeof(ApiSicherungseinstellungenVertragTests).Assembly.GetManifestResourceStream(name);
        Assert.NotNull(stream);
        return XDocument.Load(stream!);
    }

    [Fact]
    public void Sicherungspolitikdialog_enthaelt_nur_explizite_policyeingaben()
    {
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var window = Resource("AndyTours.ApiSicherungseinstellungenFenster.xaml").Root!;
        Assert.Equal("de-DE", (string?)window.Attribute("Language"));
        var fields = window.Descendants(p + "TextBox").ToArray();
        Assert.Equal(2, fields.Length);
        Assert.All(fields, field =>
        {
            var binding = (string?)field.Attribute("Text");
            Assert.Contains("Mode=TwoWay", binding);
            Assert.Contains("UpdateSourceTrigger=PropertyChanged", binding);
            Assert.Contains("ValidatesOnExceptions=True", binding);
            Assert.Contains("NotifyOnValidationError=True", binding);
        });
        Assert.Contains(window.Descendants(p + "TextBlock"), element =>
            ((string?)element.Attribute("Text"))?.Contains("StartsSeitSicherung, Mode=OneWay", StringComparison.Ordinal) == true);
        Assert.Contains(window.Descendants(p + "TextBlock"), element =>
            ((string?)element.Attribute("Text"))?.Contains("Programmstarts des Backends", StringComparison.Ordinal) == true);
        var saveButton = Assert.Single(window.Descendants(p + "Button"), button => (string?)button.Attribute("Content") == "Speichern");
        var validationTriggers = saveButton.Descendants(p + "DataTrigger").ToArray();
        Assert.Contains(validationTriggers, trigger =>
        {
            var binding = (string?)trigger.Attribute("Binding");
            return binding?.Contains("Validation.HasError", StringComparison.Ordinal) == true &&
                   binding.Contains("ElementName=IntervallFeld", StringComparison.Ordinal) &&
                   trigger.Descendants(p + "Setter").Any(setter =>
                       (string?)setter.Attribute("Property") == "IsEnabled" && (string?)setter.Attribute("Value") == "False");
        });
        Assert.Contains(validationTriggers, trigger =>
            ((string?)trigger.Attribute("Binding"))?.Contains("ElementName=HoechstzahlFeld", StringComparison.Ordinal) == true &&
            trigger.Descendants(p + "Setter").Any(setter =>
                (string?)setter.Attribute("Property") == "IsEnabled" && (string?)setter.Attribute("Value") == "False"));
        Assert.Contains(window.Descendants(p + "Button"), element => (string?)element.Attribute("Content") == "Neu laden");
        Assert.Contains(window.Descendants(p + "Button"), element => (string?)element.Attribute("Content") == "Schließen");
        Assert.DoesNotContain(window.Descendants(p + "TextBox"), field =>
            ((string?)field.Attribute("Text"))?.Contains("Ordner", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(window.Descendants(p + "Button"), button =>
            ((string?)button.Attribute("Content"))?.Contains("Sicherung starten", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Sicherungseinstellungen_sind_im_Extrasmenu_und_dialog_lifecycle_ist_geschuetzt()
    {
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var main = Resource("AndyTours.ApiHauptfenster.xaml");
        var extras = Assert.Single(main.Descendants(p + "MenuItem"), item => (string?)item.Attribute("Header") == "Extras");
        Assert.Contains(extras.Elements(p + "MenuItem"), item =>
            (string?)item.Attribute("Header") == "Sicherungseinstellungen…" &&
            (string?)item.Attribute("Click") == "Sicherungseinstellungen_Click");

        var code = ResourceText("AndyTours.ApiHauptfenster.xaml.cs");
        var start = code.IndexOf("private async void Sicherungseinstellungen_Click", StringComparison.Ordinal);
        var end = code.IndexOf("private async void GarminImport_Click", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        Assert.Contains("if (_geschlossen || _verwaltungOffen || _viewModel.Laedt || _viewModel.EditorOffen) return;", code[start..end]);
        Assert.Contains("SicherungseinstellungenAngefordert", code[start..end]);
        var composition = ResourceText("AndyTours.App.Api.cs");
        Assert.Contains("uebergebeneSitzung.Sicherungskonfiguration", composition);
        Assert.Contains("ApiSicherungseinstellungenFenster(vm) { Owner = fenster }", composition);
        var dialogCode = ResourceText("AndyTours.ApiSicherungseinstellungenFenster.xaml.cs");
        Assert.Contains("if (_viewModel.Aendert) { e.Cancel = true; return; }", dialogCode);
        Assert.Contains("_viewModel.BeginClose();", dialogCode);
    }

    private static string ResourceText(string name)
    {
        using var stream = typeof(ApiSicherungseinstellungenVertragTests).Assembly.GetManifestResourceStream(name);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}
