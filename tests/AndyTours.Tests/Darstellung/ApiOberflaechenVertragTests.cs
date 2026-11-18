namespace AndyTours.Tests.Darstellung;

using System.Globalization;
using System.Xml.Linq;
using Xunit;

/// <summary>Prüft visuelle Struktur, Bindings, Ressourcen und Skalierungsanmeldung der API-Oberfläche.</summary>
public sealed class ApiOberflaechenVertragTests
{
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string App = ReadResource("AndyTours.App.xaml");
    private static readonly string Styles = ReadResource("AndyTours.ApiOberflaeche.xaml");
    private static readonly string Main = ReadResource("AndyTours.ApiHauptfenster.xaml");
    private static readonly string Overview = ReadResource("AndyTours.ApiUebersichtViewModel.cs");
    private static readonly string Pulse = ReadResource("AndyTours.ApiPulsDarstellung.cs");
    private static readonly string MainCode = ReadResource("AndyTours.ApiHauptfenster.xaml.cs");
    private static readonly string FitCode = ReadResource("AndyTours.ApiFitImportFenster.xaml.cs");
    private static readonly string GarminCode = ReadResource("AndyTours.ApiGarminImportFenster.xaml.cs");
    private static readonly string ArchiveCode = ReadResource("AndyTours.ApiSicherungsarchivFenster.xaml.cs");
    private static readonly string SettingsCode = ReadResource("AndyTours.ApiSicherungseinstellungenFenster.xaml.cs");
    private static readonly string PreviewCode = ReadResource("AndyTours.ApiWiederherstellungsvorschauFenster.xaml.cs");
    private static readonly string BackendCode = ReadResource("AndyTours.BackendVerbindungFenster.xaml.cs");
    private static readonly string[] ExpectedMenuHeaders = ["Datei", "Extras", "Stammdaten", "Garmin Connect"];
    private static readonly string[] ExpectedMenuClicks =
    [
        "TourPlanen_Click", "RoutingKonfiguration_Click", "OverlaysOeffnen_Click", "FitDateienImportieren_Click", "GarminGeraetImportieren_Click",
        "EquipmentEinsatz_Click", "Sicherungseinstellungen_Click", "Sicherungsarchiv_Click", "Wiederherstellungsvorschau_Click",
        "AktivitaetenVerwalten_Click", "KategorienVerwalten_Click", "EquipmentsVerwalten_Click", "WartungenVerwalten_Click",
        "TrainingsbereicheVerwalten_Click", "TaetigkeitenVerwalten_Click", "SaisonzieleVerwalten_Click", "ProfilBearbeiten_Click",
        "GarminImport_Click", "GarminEinstellungen_Click"
    ];
    private static readonly string[] WindowCode = [FitCode, GarminCode, ArchiveCode, SettingsCode, PreviewCode, BackendCode];
    private static readonly string[] ExpectedTourHeaders =
    [
        "Datum", "Start", "Name", "Aktivität", "Kategorien", "Equipment", "km", "hm", "Fahrzeit",
        "Ø km/h", "max km/h", "Ø Puls", "max Puls", "Trittfrequenz", "°C", "max Höhe", "Bereich", "Kommentar"
    ];

    [Fact]
    public void SharedStylesAreNamedScopedAndMergedWithoutChangingFluentMode()
    {
        var app = XDocument.Parse(App);
        Assert.Equal("Light", (string?)app.Root?.Attribute("ThemeMode"));
        Assert.Contains(app.Descendants(Wpf + "ResourceDictionary"),
            dictionary => (string?)dictionary.Attribute("Source") == "Ressourcen/ApiOberflaeche.xaml");

        var styles = XDocument.Parse(Styles);
        var namedStyles = styles.Descendants(Wpf + "Style").ToArray();
        Assert.NotEmpty(namedStyles);
        Assert.All(namedStyles, style => Assert.True(((string?)style.Attribute(X + "Key"))?.StartsWith("Api", StringComparison.Ordinal) == true));
        Assert.Contains("ApiKartenrahmen", Styles, StringComparison.Ordinal);
        Assert.Contains("ApiPrimaeraktion", Styles, StringComparison.Ordinal);
        Assert.Contains("#FFF0F2F5", Styles, StringComparison.Ordinal);
        Assert.Contains("#FFFFFFFF", Styles, StringComparison.Ordinal);
        Assert.Contains(styles.Descendants(Wpf + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "CellStyle" &&
            (string?)setter.Attribute("Value") == "{DynamicResource {x:Type DataGridCell}}");
        Assert.DoesNotContain("<ControlTemplate", Styles, StringComparison.Ordinal);
        Assert.DoesNotContain("<Style TargetType=", Styles, StringComparison.Ordinal);

        var fit = XDocument.Parse(ReadResource("AndyTours.ApiFitImportFenster.xaml"));
        var localKeys = fit.Descendants().Attributes(X + "Key").Select(attribute => attribute.Value).ToHashSet(StringComparer.Ordinal);
        var applicationKeys = app.Descendants().Attributes(X + "Key").Select(attribute => attribute.Value).ToHashSet(StringComparer.Ordinal);
        var mergedKeys = styles.Descendants().Attributes(X + "Key").Select(attribute => attribute.Value).ToHashSet(StringComparer.Ordinal);
        var referencedKeys = fit.Descendants().Attributes().Select(attribute => attribute.Value)
            .Where(value => value.Contains("{StaticResource ", StringComparison.Ordinal))
            .SelectMany(value => value.Split("{StaticResource ", StringSplitOptions.None).Skip(1))
            .Select(value => value.Split('}')[0]).ToHashSet(StringComparer.Ordinal);
        foreach (var key in referencedKeys)
            Assert.True(localKeys.Contains(key) || applicationKeys.Contains(key) || mergedKeys.Contains(key),
                $"FIT XAML resource '{key}' is unresolved across local and merged application dictionaries.");
    }

    [Fact]
    public void MainSeparatesTitleNavigationFiltersAndPreservesTourTableContract()
    {
        var document = XDocument.Parse(Main);
        Assert.Equal("{StaticResource ApiFlaeche}", (string?)document.Root?.Attribute("Background"));
        var menu = Assert.Single(document.Descendants(Wpf + "Menu"));
        Assert.Equal(ExpectedMenuHeaders, menu.Elements(Wpf + "MenuItem").Select(item => (string?)item.Attribute("Header")).ToArray());
        Assert.Equal(ExpectedMenuClicks,
            menu.Descendants(Wpf + "MenuItem").Select(item => (string?)item.Attribute("Click")).Where(click => click is not null).ToArray());
        Assert.Contains(document.Descendants(Wpf + "TextBlock"), element => (string?)element.Attribute(X + "Name") == "BackendAdresseText");
        Assert.Contains(document.Descendants(Wpf + "DataGrid"), grid => (string?)grid.Attribute(X + "Name") == "TourenDataGrid");
        var tours = Assert.Single(document.Descendants(Wpf + "DataGrid"), grid => (string?)grid.Attribute(X + "Name") == "TourenDataGrid");
        Assert.Equal("True", (string?)tours.Attribute("CanUserSortColumns"));
        Assert.Equal("Extended", (string?)tours.Attribute("SelectionMode"));
        Assert.Equal("True", (string?)tours.Attribute("EnableRowVirtualization"));
        Assert.Equal("True", (string?)tours.Attribute("EnableColumnVirtualization"));
        Assert.Equal("{Binding AusgewaehlteTour, Mode=TwoWay}", (string?)tours.Attribute("SelectedItem"));
        Assert.Equal("TourenDataGrid_MouseDoubleClick", (string?)tours.Attribute("MouseDoubleClick"));
        Assert.Equal("TourenDataGrid_SelectionChanged", (string?)tours.Attribute("SelectionChanged"));
        Assert.Equal("TourenDataGrid_Sorting", (string?)tours.Attribute("Sorting"));
        Assert.Equal("TourenDataGrid_PreviewKeyDownMehrfach", (string?)tours.Attribute("PreviewKeyDown"));
        var columns = tours.Element(Wpf + "DataGrid.Columns")!.Elements().ToArray();
        var headers = columns.Select(column => (string?)column.Attribute("Header")).ToArray();
        Assert.Equal(12, columns.Count(c => c.Name.LocalName == "Zelltextspalte"));
        Assert.Equal(3, columns.Count(c => c.Name == Wpf + "DataGridTemplateColumn"));
        Assert.Equal(3, columns.Count(c => c.Name == Wpf + "DataGridTextColumn" && (string?)c.Attribute("IsReadOnly") == "True"));
        Assert.Equal(ExpectedTourHeaders, headers);
        Assert.Contains("TourBestDistance", Main, StringComparison.Ordinal);
        Assert.Contains("TourBestHeight", Main, StringComparison.Ordinal);
        Assert.Contains("Tabellenfilter.ResetZuruecksetzenCommand", Main, StringComparison.Ordinal);
        Assert.Contains("LoeschenMehrfach_Click", Main, StringComparison.Ordinal);
        Assert.Contains("StatusText", Main, StringComparison.Ordinal);
        Assert.Contains("Diagramm", Main, StringComparison.Ordinal);
        var filterExpander = Assert.Single(document.Descendants(Wpf + "Expander"), element => (string?)element.Attribute("Header") == "Filter");
        Assert.Contains(filterExpander.Descendants(Wpf + "ScrollViewer"), viewer => (string?)viewer.Attribute("MaxHeight") == "136");
        var overview = Assert.Single(document.Descendants(Wpf + "TabItem"), item => (string?)item.Attribute("Header") == "Übersicht");
        Assert.NotEmpty(overview.Elements(Wpf + "ScrollViewer"));
        var tourTab = Assert.Single(document.Descendants(Wpf + "TabItem"), item => (string?)item.Attribute("Header") == "Touren");
        var virtualizedGrid = Assert.Single(tourTab.Descendants(Wpf + "DataGrid"), grid => (string?)grid.Attribute(X + "Name") == "TourenDataGrid");
        Assert.Empty(virtualizedGrid.Ancestors(Wpf + "ScrollViewer"));
        Assert.Equal("True", (string?)virtualizedGrid.Attribute("EnableRowVirtualization"));
        Assert.Contains(tourTab.Descendants(Wpf + "Expander"), element => (string?)element.Attribute("Header") == "Filter und Aktionen");
        Assert.Contains("Binding Touren.Count", Main, StringComparison.Ordinal);
        Assert.Contains("Binding Laedt", Main, StringComparison.Ordinal);
        var emptyState = Assert.Single(tourTab.Descendants(Wpf + "Border"), border =>
            border.Descendants(Wpf + "TextBlock").Any(text => (string?)text.Attribute("Text") == "Keine Touren angezeigt."));
        var emptyTrigger = Assert.Single(emptyState.Descendants(Wpf + "MultiDataTrigger"));
        Assert.Contains(emptyTrigger.Descendants(Wpf + "Condition"), condition =>
            (string?)condition.Attribute("Binding") == "{Binding Touren.Count}" && (string?)condition.Attribute("Value") == "0");
        Assert.Contains(emptyTrigger.Descendants(Wpf + "Condition"), condition =>
            (string?)condition.Attribute("Binding") == "{Binding Laedt}" && (string?)condition.Attribute("Value") == "False");
    }

    [Theory]
    [InlineData("AndyTours.ApiFitImportFenster.xaml", "Schließen")]
    [InlineData("AndyTours.ApiGarminImportFenster.xaml", null)]
    [InlineData("AndyTours.ApiSicherungsarchivFenster.xaml", "Schließen")]
    [InlineData("AndyTours.ApiSicherungseinstellungenFenster.xaml", "Schließen")]
    [InlineData("AndyTours.ApiWiederherstellungsvorschauFenster.xaml", "Schließen")]
    [InlineData("AndyTours.BackendVerbindungFenster.xaml", "Abbrechen")]
    public void DialogsKeepScrollableContentAndFixedFooter(string resourceName, string? cancelCaption)
    {
        var xml = XDocument.Parse(ReadResource(resourceName));
        Assert.Equal("{StaticResource ApiFlaeche}", (string?)xml.Root?.Attribute("Background"));
        Assert.True(double.TryParse((string?)xml.Root?.Attribute("MinWidth"), NumberStyles.Float, CultureInfo.InvariantCulture, out var minWidth) && minWidth >= 420);
        Assert.Equal("CanResize", (string?)xml.Root?.Attribute("ResizeMode"));
        Assert.NotEmpty(xml.Descendants(Wpf + "ScrollViewer"));
        var footerButtons = xml.Descendants(Wpf + "Button").Where(button => !button.Ancestors(Wpf + "ScrollViewer").Any()).ToArray();
        Assert.NotEmpty(footerButtons);
        Assert.Contains(xml.Descendants(Wpf + "WrapPanel"), panel =>
            !panel.Ancestors(Wpf + "ScrollViewer").Any() &&
            (string?)panel.Attribute("HorizontalAlignment") == "Right" &&
            panel.Descendants(Wpf + "Button").Any());
        if (cancelCaption is not null)
            Assert.Contains(footerButtons, button => (string?)button.Attribute("Content") == cancelCaption);
        Assert.Contains(xml.Descendants(Wpf + "TextBlock"), text =>
            (((string?)text.Attribute("Text"))?.Contains("Binding Status", StringComparison.Ordinal) == true)
            || (((string?)text.Attribute("Text"))?.Contains("StatusText", StringComparison.Ordinal) == true)
            || (string?)text.Attribute(X + "Name") == "StatusText");
    }

    [Fact]
    public void DialogActionsStayOutsideScrollableContent()
    {
        AssertButtonOutsideScroll("AndyTours.ApiFitImportFenster.xaml", "{Binding LadenCommand}");
        AssertButtonOutsideScroll("AndyTours.ApiFitImportFenster.xaml", "{Binding HochladenCommand}");
        AssertButtonOutsideScroll("AndyTours.ApiFitImportFenster.xaml", "{Binding PruefenCommand}");
        AssertButtonOutsideScroll("AndyTours.ApiGarminImportFenster.xaml", "{Binding StartenCommand}");
        AssertButtonOutsideScroll("AndyTours.ApiGarminImportFenster.xaml", "{Binding AbbrechenCommand}");
        AssertButtonOutsideScroll("AndyTours.ApiGarminImportFenster.xaml", "{Binding PruefenCommand}");
        AssertButtonOutsideScroll("AndyTours.ApiSicherungsarchivFenster.xaml", "{Binding ErstellenCommand}");
        AssertButtonOutsideScroll("AndyTours.ApiSicherungseinstellungenFenster.xaml", "{Binding SpeichernCommand}");
        AssertButtonOutsideScroll("AndyTours.ApiSicherungseinstellungenFenster.xaml", "{Binding LadenCommand}");
        AssertButtonOutsideScroll("AndyTours.ApiWiederherstellungsvorschauFenster.xaml", "{Binding LadenCommand}");
        AssertButtonOutsideScroll("AndyTours.ApiWiederherstellungsvorschauFenster.xaml", "{Binding VorschauCommand}");
        AssertButtonOutsideScroll("AndyTours.ApiFitImportFenster.xaml", "{Binding GeraeteLesenCommand}");
        var garmin = XDocument.Parse(ReadResource("AndyTours.ApiGarminImportFenster.xaml"));
        var garminFooter = Assert.Single(garmin.Descendants(Wpf + "WrapPanel"), panel =>
            !panel.Ancestors(Wpf + "ScrollViewer").Any() &&
            panel.Descendants(Wpf + "Button").Any(button => (string?)button.Attribute("Command") == "{Binding StartenCommand}"));
        Assert.Equal("Right", (string?)garminFooter.Attribute("HorizontalAlignment"));
        var backend = XDocument.Parse(ReadResource("AndyTours.BackendVerbindungFenster.xaml"));
        Assert.Empty(Assert.Single(backend.Descendants(Wpf + "Button"), button => (string?)button.Attribute("Click") == "Verbinden_Click").Ancestors(Wpf + "ScrollViewer"));
        Assert.Empty(Assert.Single(backend.Descendants(Wpf + "Button"), button => (string?)button.Attribute("Click") == "Abbrechen_Click").Ancestors(Wpf + "ScrollViewer"));
        var backendRadios = backend.Descendants(Wpf + "RadioButton").ToArray();
        Assert.NotEmpty(backendRadios);
        Assert.All(backendRadios, radio => Assert.NotEmpty(radio.Ancestors(Wpf + "WrapPanel")));
        var archive = XDocument.Parse(ReadResource("AndyTours.ApiSicherungsarchivFenster.xaml"));
        var longAction = Assert.Single(archive.Descendants(Wpf + "Button"), button =>
            (string?)button.Attribute("Command") == "{Binding ErstellenCommand}");
        Assert.Contains(longAction.Descendants(Wpf + "TextBlock"), text => (string?)text.Attribute("TextWrapping") == "Wrap");
    }

    [Fact]
    public void BackupSafetyAndValidationControlsRemainExplicit()
    {
        var fit = XDocument.Parse(ReadResource("AndyTours.ApiFitImportFenster.xaml"));
        Assert.Contains("Dateien.Count", fit.ToString(), StringComparison.Ordinal);
        Assert.Contains("FIT prüfen / übernehmen…", fit.ToString(), StringComparison.Ordinal);
        var garmin = XDocument.Parse(ReadResource("AndyTours.ApiGarminImportFenster.xaml"));
        Assert.Contains("Kandidaten.Count", garmin.ToString(), StringComparison.Ordinal);
        Assert.Contains(garmin.Descendants(Wpf + "Button"), button => (string?)button.Attribute("Command") == "{Binding PruefenCommand}");

        var archive = XDocument.Parse(ReadResource("AndyTours.ApiSicherungsarchivFenster.xaml"));
        Assert.Contains("keine Wiederherstellung", archive.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("keinen Download", archive.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Noch kein Archiv-Ergebnis", archive.ToString(), StringComparison.Ordinal);
        var create = Assert.Single(archive.Descendants(Wpf + "Button"), button => (string?)button.Attribute("Command") == "{Binding ErstellenCommand}");
        Assert.Empty(create.Ancestors(Wpf + "ScrollViewer"));

        var settings = XDocument.Parse(ReadResource("AndyTours.ApiSicherungseinstellungenFenster.xaml"));
        Assert.Contains("{Binding SpeichernCommand}", settings.ToString(), StringComparison.Ordinal);
        var save = Assert.Single(settings.Descendants(Wpf + "Button"), button => (string?)button.Attribute("Command") == "{Binding SpeichernCommand}");
        Assert.Empty(save.Ancestors(Wpf + "ScrollViewer"));
        Assert.Equal(2, settings.Descendants(Wpf + "DataTrigger")
            .Count(trigger => (string?)trigger.Attribute("Value") == "True" &&
                (((string?)trigger.Attribute("Binding"))?.Contains("Validation.HasError", StringComparison.Ordinal) ?? false)));

        var preview = XDocument.Parse(ReadResource("AndyTours.ApiWiederherstellungsvorschauFenster.xaml"));
        Assert.Contains("keine Wiederherstellung freigegeben", preview.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Text=\"Nicht ausgeführt\"", preview.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyCommand", preview.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("RestoreCommand", preview.ToString(), StringComparison.Ordinal);
        Assert.Contains(preview.Descendants(Wpf + "DataGrid"), grid => (string?)grid.Attribute("IsReadOnly") == "True");
        Assert.Contains("{Binding Vorschau.Hinweise, Mode=OneWay}", preview.ToString(), StringComparison.Ordinal);
        Assert.Contains("Eintraege.Count", preview.ToString(), StringComparison.Ordinal);
        var request = Assert.Single(preview.Descendants(Wpf + "Button"), button => (string?)button.Attribute("Command") == "{Binding VorschauCommand}");
        Assert.Empty(request.Ancestors(Wpf + "ScrollViewer"));
    }

    [Fact]
    public void EveryApiWindowRegistersExistingScalingImmediatelyAfterInitialization()
    {
        var normalizedMainCode = MainCode.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("InitializeComponent();\n        AndyTours.App.Funktionen.Oberflaechenskalierung.Anmelden(this, groesseUmrechnen: false);",
            normalizedMainCode, StringComparison.Ordinal);
        foreach (var code in WindowCode)
        {
            var normalizedCode = code.Replace("\r\n", "\n", StringComparison.Ordinal);
            Assert.Contains("InitializeComponent();\n        AndyTours.App.Funktionen.Oberflaechenskalierung.Anmelden(this);",
                normalizedCode, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ChartsUseLegibleAxesAndQuietGridWithoutChangingMetricMapping()
    {
        Assert.Contains("MajorGridlineStyle = LineStyle.Solid", Overview, StringComparison.Ordinal);
        Assert.Contains("MajorGridlineColor = OxyColor.FromRgb(232, 236, 240)", Overview, StringComparison.Ordinal);
        Assert.Contains("FillColor = OxyColor.FromRgb(36, 109, 178)", Overview, StringComparison.Ordinal);
        Assert.Contains("periode.FahrzeitSekunden / 3600d", Overview, StringComparison.Ordinal);
        Assert.Contains("ApiDiagrammkennzahl.Kilometer => periode.DistanzKm", Overview, StringComparison.Ordinal);
        Assert.Contains("ApiDiagrammkennzahl.Hoehenmeter => periode.Hoehenmeter", Overview, StringComparison.Ordinal);
        Assert.Contains("ApiDiagrammkennzahl.Anzahl => periode.Anzahl", Overview, StringComparison.Ordinal);

        Assert.Contains("MajorGridlineStyle = LineStyle.Solid", Pulse, StringComparison.Ordinal);
        Assert.Contains("cell.Sekunden / 60d", Pulse, StringComparison.Ordinal);
        Assert.Contains("OrderBy(row => row.PeriodIndex).ThenBy(row => row.Area)", Pulse, StringComparison.Ordinal);
        Assert.Contains("antwort.Zellen.ToDictionary", Pulse, StringComparison.Ordinal);
    }

    /// <summary>Prüft, dass Dialogaktionen nicht durch den rollbaren Inhaltsbereich verschwinden.</summary>
    private static void AssertButtonOutsideScroll(string logicalName, string command)
    {
        var document = XDocument.Parse(ReadResource(logicalName));
        var button = Assert.Single(document.Descendants(Wpf + "Button"),
            element => (string?)element.Attribute("Command") == command);
        Assert.Empty(button.Ancestors(Wpf + "ScrollViewer"));
    }

    private static string ReadResource(string logicalName)
    {
        using var stream = typeof(ApiOberflaechenVertragTests).Assembly.GetManifestResourceStream(logicalName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}
