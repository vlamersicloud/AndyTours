using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiRoutingKonfigurationFensterVertragTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string[] Commands =
    [
        "LadenCommand", "AbbrechenCommand", "DienstSpeichernCommand", "AdresseSpeichernCommand",
        "AdresseZuruecksetzenCommand", "OrsSchluesselSpeichernCommand", "OrsSchluesselEntfernenCommand"
    ];

    [Fact]
    public void Root_menu_contains_one_routing_settings_action_under_file()
    {
        var root = Load("AndyTours.ApiHauptfenster.xaml");
        var item = Assert.Single(root.Descendants(P + "MenuItem"), node => (string?)node.Attribute("Click") == "RoutingKonfiguration_Click");
        Assert.Equal("Routing-Einstellungen…", (string?)item.Attribute("Header"));
        Assert.Equal("Datei", (string?)item.Parent?.Attribute("Header"));
    }

    [Fact]
    public void Routing_settings_window_exposes_explicit_operations_and_safe_password_input()
    {
        var document = Load("AndyTours.ApiRoutingKonfigurationFenster.xaml");
        foreach (var command in Commands)
            Assert.Contains(document.Descendants(P + "Button"), button => (string?)button.Attribute("Command") == "{Binding " + command + "}");

        Assert.Contains(document.Descendants(P + "TextBox"), node => (string?)node.Attribute("Text") == "{Binding GespeicherteAdresse, Mode=OneWay}");
        Assert.Contains(document.Descendants(P + "TextBlock"), node => (string?)node.Attribute("Text") == "{Binding AdresseStatus}");
        Assert.Contains(document.Descendants(P + "TextBlock"), node => (string?)node.Attribute("Text") == "{Binding DienstStatus}");
        Assert.Contains(document.Descendants(P + "TextBox"), node => ((string?)node.Attribute("Text"))?.Contains("AdresseEntwurf", StringComparison.Ordinal) == true);
        var password = Assert.Single(document.Descendants(P + "PasswordBox"));
        Assert.Equal("OrsSchluesselFeld_PasswordChanged", (string?)password.Attribute("PasswordChanged"));
        Assert.Null(password.Attribute("Password"));
        Assert.DoesNotContain(document.Descendants(P + "TextBox"), box => ((string?)box.Attribute("Text"))?.Contains("Schluessel", StringComparison.OrdinalIgnoreCase) == true);
        Assert.All(document.Descendants(P + "TextBox").Where(box => box.Attribute("Text")?.Value.Contains("AdresseEntwurf", StringComparison.Ordinal) == true),
            box => Assert.Equal("{Binding KannBearbeiten}", (string?)box.Attribute("IsEnabled")));
        Assert.NotNull(document.Root!.Attribute("Width"));
        Assert.NotNull(document.Root.Attribute("Height"));
        Assert.Contains(document.Descendants(P + "ScrollViewer"), _ => true);

        var localKeys = document.Descendants(P + "Window.Resources").Elements()
            .Select(node => (string?)node.Attribute(X + "Key")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var sharedKeys = Keys(Load("AndyTours.App.xaml"));
        sharedKeys.UnionWith(Keys(Load("AndyTours.ApiOberflaeche.xaml")));
        var references = document.Descendants().Attributes().Select(attribute => attribute.Value)
            .Where(value => value.Contains("{StaticResource ", StringComparison.Ordinal))
            .SelectMany(value => value.Split("{StaticResource ", StringSplitOptions.None).Skip(1))
            .Select(value => value.Split('}')[0]).ToHashSet(StringComparer.Ordinal);
        Assert.All(references, key => Assert.True(localKeys.Contains(key) || sharedKeys.Contains(key),
            $"Unresolved resource key: {key}"));
        Assert.All(localKeys, key => Assert.Contains(key, references));
    }

    private static XDocument Load(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        Assert.NotNull(stream);
        return XDocument.Load(stream!);
    }

    private static HashSet<string> Keys(XDocument document) =>
        document.Root!.DescendantsAndSelf().Attributes(X + "Key").Select(attribute => attribute.Value).ToHashSet(StringComparer.Ordinal);
}
