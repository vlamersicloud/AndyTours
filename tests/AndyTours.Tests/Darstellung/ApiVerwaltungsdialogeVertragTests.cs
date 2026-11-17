using System.Globalization;
using System.Reflection;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

/// <summary>Vertragstests für die zehn Verwaltungsdialoge und ihre unveränderten Sicherheitsbindungen.</summary>
public sealed class ApiVerwaltungsdialogeVertragTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string[] Dialoge =
    [
        "AndyTours.ApiAktivitaetenFenster.xaml", "AndyTours.ApiKategorienFenster.xaml",
        "AndyTours.ApiEquipmentsFenster.xaml", "AndyTours.ApiWartungsFenster.xaml",
        "AndyTours.ApiEquipmentEinsatzFenster.xaml", "AndyTours.ApiTextlisteFenster.xaml",
        "AndyTours.ApiRoutingKonfigurationFenster.xaml", "AndyTours.ApiGarminEinstellungenFenster.xaml",
        "AndyTours.ApiTourenMehrfachFenster.xaml", "AndyTours.ApiOverlaysFenster.xaml"
    ];
    private static readonly string[] CodeBehind =
    [
        "AndyTours.ApiAktivitaetenFenster.xaml.cs", "AndyTours.ApiKategorienFenster.xaml.cs",
        "AndyTours.ApiEquipmentsFenster.xaml.cs", "AndyTours.ApiWartungsFenster.xaml.cs",
        "AndyTours.ApiEquipmentEinsatzFenster.xaml.cs", "AndyTours.ApiTextlisteFenster.xaml.cs",
        "AndyTours.ApiRoutingKonfigurationFenster.xaml.cs", "AndyTours.ApiGarminEinstellungenFenster.xaml.cs",
        "AndyTours.ApiTourenMehrfachFenster.xaml.cs", "AndyTours.ApiOverlaysFenster.xaml.cs"
    ];
    private static readonly string[] EintragFenster = ["AndyTours.ApiAktivitaetenFenster.xaml", "AndyTours.ApiKategorienFenster.xaml", "AndyTours.ApiEquipmentsFenster.xaml"];
    private static readonly string[] AktivitaetsSpalten = ["Name", "Haupt", "Einheit", "Ziel km", "Ziel hm", "Ziel Anzahl"];
    private static readonly string[] KategorieSpalten = ["Name", "Standard"];
    private static readonly string[] EquipmentSpalten = ["Name", "Aktivität", "Kategorie", "Standard", "Ausgemustert", "Anschaffung", "Anfang km"];
    private static readonly string[] AktivitaetsCommands = ["LadenCommand", "AnlegenCommand", "UmbenennenCommand", "LoeschenCommand", "HauptSetzenCommand", "NachObenCommand", "NachUntenCommand", "ZusammenfuehrenCommand"];
    private static readonly string[] KategorieCommands = ["LadenCommand", "AnlegenCommand", "UmbenennenCommand", "LoeschenCommand", "StandardUmschaltenCommand", "ZusammenfuehrenCommand"];
    private static readonly string[] EquipmentCommands = ["LadenCommand", "NeuCommand", "BearbeitenCommand", "LoeschenCommand"];
    private static readonly string[] TextlistenCommands =
        ["LadenCommand", "AnlegenCommand", "UmbenennenCommand", "LoeschenCommand", "NachObenCommand", "NachUntenCommand"];
    private static readonly string[] RoutingCommands =
        ["LadenCommand", "AbbrechenCommand", "DienstSpeichernCommand", "AdresseSpeichernCommand",
         "AdresseZuruecksetzenCommand", "OrsSchluesselSpeichernCommand", "OrsSchluesselEntfernenCommand"];
    private static readonly string[] RoutingFooterCommands = ["{Binding LadenCommand}", "{Binding AbbrechenCommand}"];
    private static readonly string[] RoutingContentCommands =
        ["DienstSpeichernCommand", "AdresseSpeichernCommand", "AdresseZuruecksetzenCommand",
         "OrsSchluesselSpeichernCommand", "OrsSchluesselEntfernenCommand"];
    private static readonly string[] OverlayCommands =
        ["LadenCommand", "OrdnerAnlegenCommand", "VerschiebenCommand", "LoeschenCommand",
         "GpxHinzufuegenCommand", "GpxImportierenCommand", "GpxAuswahlVerwerfenCommand", "NameUebernehmenCommand"];
    private static readonly string[] WartungsCommands =
        ["LadenCommand", "NeueWartungCommand", "WartungBearbeitenCommand", "WartungenLoeschenCommand",
         "NeuerAlarmCommand", "AlarmBearbeitenCommand", "AlarmeLoeschenCommand", "AlarmeUmschaltenCommand"];
    private static readonly string[] GarminCommands =
        ["SchluesselSpeichernCommand", "SchluesselEntfernenCommand", "AutomatikSpeichernCommand", "LadenCommand"];
    private static readonly string[] BulkCommands = ["AusfuehrenCommand", "AbbrechenCommand"];

    /// <summary>Prüft die tatsächlichen Window-/App-Ressourcen und deren statische XAML-Verweise.</summary>
    [Fact]
    public void DialogResourcesResolveAgainstWindowAndMergedApplicationDictionaries()
    {
        var app = XDocument.Parse(Read("AndyTours.App.xaml"));
        var shared = XDocument.Parse(Read("AndyTours.ApiOberflaeche.xaml"));
        var appKeys = Keys(app).Concat(Keys(shared)).ToHashSet(StringComparer.Ordinal);
        var tableStyle = Assert.Single(shared.Descendants(P + "Style"),
            style => (string?)style.Attribute(X + "Key") == "ApiTabelle");
        Assert.Contains(tableStyle.Elements(P + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "CellStyle" &&
            (string?)setter.Attribute("Value") == "{DynamicResource {x:Type DataGridCell}}");

        foreach (var name in Dialoge)
        {
            var document = XDocument.Parse(Read(name));
            var localKeys = Keys(document).ToHashSet(StringComparer.Ordinal);
            var references = document.Root!.DescendantsAndSelf().Attributes().Select(attribute => attribute.Value)
                .Where(value => value.Contains("{StaticResource ", StringComparison.Ordinal))
                .SelectMany(value => value.Split("{StaticResource ", StringSplitOptions.None).Skip(1))
                .Select(value => value.Split('}')[0]).ToHashSet(StringComparer.Ordinal);
            Assert.All(references, key => Assert.True(localKeys.Contains(key) || appKeys.Contains(key),
                $"{name}: unresolved resource '{key}' across local and merged App dictionaries."));
        }
    }

    [Fact]
    public void TenWindowsKeepScrollableContentAndReachableFixedCloseActions()
    {
        foreach (var name in Dialoge)
        {
            var document = XDocument.Parse(Read(name));
            var root = Assert.IsType<XElement>(document.Root);
            Assert.Equal("{StaticResource ApiFlaeche}", (string?)root.Attribute("Background"));
            Assert.True(double.TryParse((string?)root.Attribute("MinWidth"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var minWidth) && minWidth >= 400 && minWidth <= 640, $"{name}: minimum width must be explicit.");
            var close = Assert.Single(document.Descendants(P + "Button"), button =>
                (string?)button.Attribute("Content") == "Schließen" && (string?)button.Attribute("IsCancel") == "True");
            Assert.Empty(close.Ancestors(P + "ScrollViewer"));
            Assert.Contains(document.Descendants(P + "WrapPanel"), panel =>
                !panel.Ancestors(P + "ScrollViewer").Any() && panel.Descendants(P + "Button").Any());
        }
    }

    [Fact]
    public void EntityAndMaintenanceTablesRemainReadOnlyBoundedAndVersionSafe()
    {
        for (var index = 0; index < EintragFenster.Length; index++)
        {
            var name = EintragFenster[index];
            var document = XDocument.Parse(Read(name));
            var grid = Assert.Single(document.Descendants(P + "DataGrid"));
            Assert.Equal("{StaticResource ApiTabelle}", (string?)grid.Attribute("Style"));
            Assert.Equal("True", (string?)grid.Attribute("IsReadOnly"));
            Assert.Equal("True", (string?)grid.Attribute("EnableRowVirtualization"));
            Assert.Equal("True", (string?)grid.Attribute("EnableColumnVirtualization"));
            Assert.Equal("{Binding Eintraege}", (string?)grid.Attribute("ItemsSource"));
            var expected = index switch
            {
                0 => AktivitaetsSpalten,
                1 => KategorieSpalten,
                _ => EquipmentSpalten
            };
            Assert.Equal(expected, grid.Element(P + "DataGrid.Columns")!.Elements()
                .Select(column => (string?)column.Attribute("Header")).ToArray());
            var commands = index switch { 0 => AktivitaetsCommands, 1 => KategorieCommands, _ => EquipmentCommands };
            foreach (var command in commands)
                Assert.Contains(document.Descendants(P + "Button"), button =>
                    (string?)button.Attribute("Command") == "{Binding " + command + "}");
            Assert.Empty(grid.Ancestors(P + "ScrollViewer"));
            if (index < 2)
            {
                Assert.Contains(document.Descendants(P + "Expander"), expander =>
                    ((string?)expander.Attribute("Header"))?.Contains("Editor", StringComparison.Ordinal) == true &&
                    expander.Descendants(P + "ScrollViewer").Any(scroll => (string?)scroll.Attribute("Height") == "104") &&
                    expander.Descendants(P + "TextBox").Any(box =>
                        ((string?)box.Attribute("Text"))?.Contains("NameText", StringComparison.Ordinal) == true) &&
                    expander.Descendants(P + "ComboBox").Any(combo =>
                        ((string?)combo.Attribute("ItemsSource")) == "{Binding ZielOptionen}"));
                Assert.Contains(document.Descendants(P + "Expander"), expander =>
                    ((string?)expander.Attribute("Header"))?.Contains("Aktionen", StringComparison.Ordinal) == true &&
                    expander.Descendants(P + "ScrollViewer").Any(scroll => (string?)scroll.Attribute("Height") == "104") &&
                    commands.Where(command => command != "LadenCommand").All(command =>
                        expander.Descendants(P + "Button").Any(button =>
                            (string?)button.Attribute("Command") == "{Binding " + command + "}")));
            }
            else
            {
                foreach (var command in EquipmentCommands.Where(command => command != "LadenCommand"))
                {
                    var action = Assert.Single(document.Descendants(P + "Button"),
                        button => (string?)button.Attribute("Command") == "{Binding " + command + "}");
                    Assert.Empty(action.Ancestors(P + "ScrollViewer"));
                }
            }
        }

        var maintenance = XDocument.Parse(Read("AndyTours.ApiWartungsFenster.xaml"));
        var maintenanceGrid = Assert.Single(maintenance.Descendants(P + "DataGrid"), grid => (string?)grid.Attribute(X + "Name") == "WartungenGrid");
        var alarmGrid = Assert.Single(maintenance.Descendants(P + "DataGrid"), grid => (string?)grid.Attribute(X + "Name") == "AlarmeGrid");
        Assert.Equal("{Binding Wartungszeilen}", (string?)maintenanceGrid.Attribute("ItemsSource"));
        Assert.Equal("{Binding Alarmzeilen}", (string?)alarmGrid.Attribute("ItemsSource"));
        var tableStyle = Assert.Single(maintenance.Descendants(P + "Style"),
            style => (string?)style.Attribute(X + "Key") == "Table");
        Assert.Equal("{StaticResource ApiTabelle}", (string?)tableStyle.Attribute("BasedOn"));
        Assert.Equal("{StaticResource Table}", (string?)maintenanceGrid.Attribute("Style"));
        Assert.Equal("{StaticResource Table}", (string?)alarmGrid.Attribute("Style"));
        Assert.Equal("True", (string?)Setter(tableStyle, "EnableRowVirtualization"));
        Assert.Equal("True", (string?)Setter(tableStyle, "EnableColumnVirtualization"));
        Assert.Equal("True", (string?)Setter(tableStyle, "ScrollViewer.CanContentScroll"));
        Assert.Empty(maintenanceGrid.Ancestors(P + "ScrollViewer"));
        Assert.Contains(maintenance.Descendants(P + "Expander"), expander =>
            (string?)expander.Attribute("Header") == "Hinweise zur Haltbarkeit" &&
            (string?)expander.Attribute("IsExpanded") == "False" &&
            expander.Descendants(P + "ScrollViewer").Any(scroll => (string?)scroll.Attribute("Height") == "64"));
        Assert.Contains(maintenance.Descendants(P + "TextBlock"), text =>
            ((string?)text.Attribute("Text"))?.Contains("Haltbarkeit:", StringComparison.Ordinal) == true);
        foreach (var command in WartungsCommands)
            Assert.Contains(maintenance.Descendants(P + "Button"), button =>
                (string?)button.Attribute("Command") == "{Binding " + command + "}");
    }

    [Fact]
    public void TextlistAndRoutingActionsRemainExplicitAndSecretSafe()
    {
        var textlist = XDocument.Parse(Read("AndyTours.ApiTextlisteFenster.xaml"));
        Assert.Equal("{Binding Eintraege}", (string?)Assert.Single(textlist.Descendants(P + "ListBox")).Attribute("ItemsSource"));
        Assert.Equal("{Binding Auswahl, Mode=TwoWay}", (string?)Assert.Single(textlist.Descendants(P + "ListBox")).Attribute("SelectedItem"));
        foreach (var command in TextlistenCommands)
            Assert.Contains(textlist.Descendants(P + "Button"), button => (string?)button.Attribute("Command") == "{Binding " + command + "}");

        var routing = XDocument.Parse(Read("AndyTours.ApiRoutingKonfigurationFenster.xaml"));
        foreach (var command in RoutingCommands)
            Assert.Contains(routing.Descendants(P + "Button"), button => (string?)button.Attribute("Command") == "{Binding " + command + "}");
        var routingBody = Assert.Single(routing.Descendants(P + "ScrollViewer"));
        var routingFooter = Assert.Single(routing.Descendants(P + "WrapPanel"),
            panel => (string?)panel.Attribute("Grid.Row") == "1");
        Assert.Equal(RoutingFooterCommands, routingFooter.Descendants(P + "Button")
            .Select(button => (string?)button.Attribute("Command")).Where(command => command is not null).Select(command => command!).ToArray());
        foreach (var command in RoutingContentCommands)
            Assert.Contains(routingBody.Descendants(P + "Button"),
                button => (string?)button.Attribute("Command") == "{Binding " + command + "}");
        Assert.Contains(routing.Descendants(P + "TextBox"), box =>
            (string?)box.Attribute("Text") == "{Binding GespeicherteAdresse, Mode=OneWay}" &&
            (string?)box.Attribute("IsReadOnly") == "True");
        var password = Assert.Single(routing.Descendants(P + "PasswordBox"));
        Assert.Equal("OrsSchluesselFeld_PasswordChanged", (string?)password.Attribute("PasswordChanged"));
        Assert.Null(password.Attribute("Password"));
        Assert.DoesNotContain(routing.Descendants(P + "TextBox"), box =>
            ((string?)box.Attribute("Text"))?.Contains("Schluessel", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void GarminBulkAndOverlaySafetyBindingsArePreserved()
    {
        var garmin = XDocument.Parse(Read("AndyTours.ApiGarminEinstellungenFenster.xaml"));
        var keyInput = Assert.Single(garmin.Descendants(P + "PasswordBox"));
        Assert.Equal("SchluesselFeld_PasswordChanged", (string?)keyInput.Attribute("PasswordChanged"));
        Assert.Null(keyInput.Attribute("Password"));
        Assert.Contains(garmin.Descendants(P + "DataTrigger"), trigger =>
            (string?)trigger.Attribute("Binding") == "{Binding HasKey}" &&
            trigger.Elements(P + "Setter").Any(setter => (string?)setter.Attribute("Property") == "Text"));
        foreach (var command in GarminCommands)
            Assert.Contains(garmin.Descendants(P + "Button"), button =>
                (string?)button.Attribute("Command") == "{Binding " + command + "}");

        var bulk = XDocument.Parse(Read("AndyTours.ApiTourenMehrfachFenster.xaml"));
        var execute = Assert.Single(bulk.Descendants(P + "Button"), button => (string?)button.Attribute("Content") == "Ausführen");
        Assert.Equal("{Binding AusfuehrenCommand}", (string?)execute.Attribute("Command"));
        foreach (var command in BulkCommands)
            Assert.Contains(bulk.Descendants(P + "Button"), button =>
                (string?)button.Attribute("Command") == "{Binding " + command + "}");
        Assert.NotEqual("True", (string?)execute.Attribute("IsDefault"));
        Assert.Contains(bulk.Descendants(P + "TextBlock"), text =>
            ((string?)text.Attribute("Text"))?.Contains("Beschreibung", StringComparison.Ordinal) == true &&
            (string?)text.Attribute("TextWrapping") == "Wrap");

        var overlays = XDocument.Parse(Read("AndyTours.ApiOverlaysFenster.xaml"));
        var tree = Assert.Single(overlays.Descendants(P + "TreeView"));
        Assert.Equal("{Binding IstBereit}", (string?)tree.Attribute("IsEnabled"));
        Assert.Contains(tree.Descendants(P + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "IsSelected" &&
            ((string?)setter.Attribute("Value"))?.Contains("IstAusgewaehlt, Mode=TwoWay", StringComparison.Ordinal) == true);
        Assert.Contains(tree.Descendants(P + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "IsExpanded" &&
            ((string?)setter.Attribute("Value"))?.Contains("IstAufgeklappt, Mode=TwoWay", StringComparison.Ordinal) == true);
        var queue = Assert.Single(overlays.Descendants(P + "DataGrid"));
        Assert.Equal("{Binding Importdateien}", (string?)queue.Attribute("ItemsSource"));
        Assert.Equal("120", (string?)queue.Attribute("Height"));
        Assert.Equal("True", (string?)queue.Attribute("EnableRowVirtualization"));
        Assert.DoesNotContain("Pfad", overlays.ToString(), StringComparison.Ordinal);
        foreach (var command in OverlayCommands)
            Assert.Contains(overlays.Descendants(P + "Button"), button => (string?)button.Attribute("Command") == "{Binding " + command + "}");
        Assert.Empty(tree.Ancestors(P + "ScrollViewer"));
        Assert.Empty(queue.Ancestors(P + "ScrollViewer"));
    }

    [Fact]
    public void EveryManagementWindowRegistersTheExistingScalingImmediatelyAfterInitializeComponent()
    {
        foreach (var resource in CodeBehind)
        {
            var code = Read(resource).Replace("\r\n", "\n", StringComparison.Ordinal);
            Assert.Contains("InitializeComponent();\n        ", code, StringComparison.Ordinal);
            var initialize = code.IndexOf("InitializeComponent();", StringComparison.Ordinal);
            var registration = code.IndexOf("Oberflaechenskalierung.Anmelden(this)", initialize, StringComparison.Ordinal);
            Assert.True(registration > initialize && registration - initialize < 150, $"{resource}: scaling registration must follow initialization.");
        }
    }

    private static string? Setter(XElement style, string property) =>
        (string?)style.Elements(P + "Setter").Single(setter => (string?)setter.Attribute("Property") == property).Attribute("Value");

    private static HashSet<string> Keys(XDocument document) =>
        document.Root!.DescendantsAndSelf().Attributes(X + "Key").Select(attribute => attribute.Value).ToHashSet(StringComparer.Ordinal);

    private static string Read(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}
