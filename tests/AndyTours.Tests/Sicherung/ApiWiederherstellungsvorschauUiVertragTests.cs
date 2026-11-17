namespace AndyTours.Tests.Sicherung;

using System.Xml.Linq;
using Xunit;

public sealed class ApiWiederherstellungsvorschauUiVertragTests
{
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly string DialogXaml = ReadResource("AndyTours.ApiWiederherstellungsvorschauFenster.xaml");
    private static readonly string DialogCode = ReadResource("AndyTours.ApiWiederherstellungsvorschauFenster.xaml.cs");
    private static readonly string MainXaml = ReadResource("AndyTours.ApiHauptfenster.xaml");
    private static readonly string MainCode = ReadResource("AndyTours.ApiHauptfenster.xaml.cs");
    private static readonly string Composition = ReadResource("AndyTours.App.Api.cs");
    private static readonly string Session = ReadResource("AndyTours.BackendClientSitzung.cs");
    private static readonly string Endpoints = ReadResource("AndyTours.Wiederherstellungsvorschau.Endpunkte.cs");
    private static readonly string Program = ReadResource("AndyTours.Sicherung.Erstellung.Program.cs");

    [Fact]
    public void DialogOffersOnlyMetadataPreviewAndRendersBoundHintsAndUnknownValues()
    {
        var document = XDocument.Parse(DialogXaml);
        Assert.NotNull(document.Root);
        var grid = Assert.Single(document.Descendants(Wpf + "DataGrid"));
        Assert.Equal("False", (string?)grid.Attribute("AutoGenerateColumns"));
        Assert.Equal("True", (string?)grid.Attribute("IsReadOnly"));
        Assert.Equal("{Binding Auswahl, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}", (string?)grid.Attribute("SelectedItem"));
        Assert.Contains(document.Descendants(Wpf + "DataGridTextColumn"), c => (string?)c.Attribute("Header") == "Erstellt (UTC)");
        Assert.Contains(document.Descendants(Wpf + "DataGridTextColumn"), c => ((string?)c.Attribute("Header"))?.Contains("Zeitzone unbekannt", StringComparison.Ordinal) == true);
        Assert.Contains("Nur Metadatenvorschau – keine Wiederherstellung freigegeben.", DialogXaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding}\"", DialogXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("•  {Binding}", DialogXaml, StringComparison.Ordinal);
        Assert.Contains("FallbackValue=Unbekannt / ungeprüft", DialogXaml, StringComparison.Ordinal);
        Assert.Contains("FallbackValue=Keine Auswahl", DialogXaml, StringComparison.Ordinal);
        Assert.Contains("{Binding LadenCommand}", DialogXaml, StringComparison.Ordinal);
        Assert.Contains("{Binding VorschauCommand}", DialogXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyCommand", DialogXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("RestoreCommand", DialogXaml, StringComparison.Ordinal);
        Assert.Contains("IsCancel=\"True\"", DialogXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void DialogLoadsOnlyOnceAndDisposesWithoutBlockingBusyClose()
    {
        Assert.Contains("if (_disposed || _loaded) return;", DialogCode, StringComparison.Ordinal);
        Assert.Contains("await _viewModel.LadenAsync();", DialogCode, StringComparison.Ordinal);
        Assert.DoesNotContain("VorschauAsync", DialogCode, StringComparison.Ordinal);
        Assert.Contains("Closed += ClosedWindow;", DialogCode, StringComparison.Ordinal);
        Assert.Contains("_viewModel.Dispose();", DialogCode, StringComparison.Ordinal);
        Assert.DoesNotContain("Closing +=", DialogCode, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowHasOnePreviewRouteAndRespectsSharedGuards()
    {
        var document = XDocument.Parse(MainXaml);
        var extras = Assert.Single(document.Descendants(Wpf + "MenuItem"), x => (string?)x.Attribute("Header") == "Extras");
        Assert.Single(extras.Elements(Wpf + "MenuItem"), x => (string?)x.Attribute("Click") == "Wiederherstellungsvorschau_Click");
        Assert.Contains("public event Func<Task>? WiederherstellungsvorschauAngefordert;", MainCode, StringComparison.Ordinal);
        var handler = Slice(MainCode, "private async void Wiederherstellungsvorschau_Click", "private async void GarminImport_Click");
        Assert.Contains("_geschlossen", handler, StringComparison.Ordinal);
        Assert.Contains("_lifetime.IsCancellationRequested", handler, StringComparison.Ordinal);
        Assert.Contains("_verwaltungOffen", handler, StringComparison.Ordinal);
        Assert.Contains("_viewModel.Laedt", handler, StringComparison.Ordinal);
        Assert.Contains("_viewModel.EditorOffen", handler, StringComparison.Ordinal);
        Assert.Contains("Die Wiederherstellungsvorschau konnte nicht geöffnet werden.", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void CompositionIsLazyOwnedAndContainsNoRestoreOrJournalWorkflow()
    {
        var composition = Slice(Composition,
            "fenster.WiederherstellungsvorschauAngefordert += () =>",
            "fenster.SicherungsarchivAngefordert += () =>");
        Assert.Contains("uebergebeneSitzung.Sicherungskatalog", composition, StringComparison.Ordinal);
        Assert.Contains("uebergebeneSitzung.Wiederherstellungsvorschau", composition, StringComparison.Ordinal);
        Assert.Contains("fenster.LebenszeitToken", composition, StringComparison.Ordinal);
        Assert.Contains("new ApiWiederherstellungsvorschauFenster(vm) { Owner = fenster }", composition, StringComparison.Ordinal);
        Assert.Contains("dialog.ShowDialog();", composition, StringComparison.Ordinal);
        Assert.Contains("dialog.Dispose();", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("Sicherungsjournal", composition, StringComparison.Ordinal);
        Assert.DoesNotContain(".Restore(", composition, StringComparison.Ordinal);
        Assert.DoesNotContain(".Apply(", composition, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionAndEndpointUseNonDestructivePreviewContract()
    {
        Assert.Contains("Wiederherstellungsvorschau = new WiederherstellungsvorschauHttpClient(_importHttpClient);", Session, StringComparison.Ordinal);
        Assert.Contains("Timeout = TimeSpan.FromMinutes(10)", Session, StringComparison.Ordinal);
        Assert.Contains("/wiederherstellungsvorschau/", Endpoints, StringComparison.Ordinal);
        Assert.Contains("ausschließlich nichtdestruktive Vorschau-Metadaten", Endpoints, StringComparison.Ordinal);
        Assert.Contains("keine SQLite-Prüfung", Endpoints, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExklusiverDatenbestand", Endpoints, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IWiederherstellungsvorschau>", Program, StringComparison.Ordinal);
        Assert.Contains("app.MapWiederherstellungsvorschau();", Program, StringComparison.Ordinal);
    }

    private static string Slice(string text, string start, string end)
    {
        var startAt = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startAt >= 0, $"Missing source marker: {start}");
        var endAt = text.IndexOf(end, startAt + start.Length, StringComparison.Ordinal);
        Assert.True(endAt > startAt, $"Missing source marker: {end}");
        return text[startAt..endAt];
    }

    private static string ReadResource(string logicalName)
    {
        using var stream = typeof(ApiWiederherstellungsvorschauUiVertragTests).Assembly.GetManifestResourceStream(logicalName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}
