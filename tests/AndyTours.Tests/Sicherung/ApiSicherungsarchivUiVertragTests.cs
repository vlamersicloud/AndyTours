namespace AndyTours.Tests.Sicherung;

using System.Xml.Linq;
using Xunit;

public sealed class ApiSicherungsarchivUiVertragTests
{
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly string DialogXaml = ReadResource("AndyTours.ApiSicherungsarchivFenster.xaml");
    private static readonly string Styles = ReadResource("AndyTours.ApiOberflaeche.xaml");
    private static readonly string DialogCode = ReadResource("AndyTours.ApiSicherungsarchivFenster.xaml.cs");
    private static readonly string MainXaml = ReadResource("AndyTours.ApiHauptfenster.xaml");
    private static readonly string MainCode = ReadResource("AndyTours.ApiHauptfenster.xaml.cs");
    private static readonly string Composition = ReadResource("AndyTours.App.Api.cs");
    private static readonly string Session = ReadResource("AndyTours.BackendClientSitzung.cs");

    [Fact]
    public void DialogIsReadOnlyAndOnlyStartsCreationOnExplicitCommand()
    {
        var document = XDocument.Parse(DialogXaml);
        Assert.NotNull(document.Root);
        Assert.NotEmpty(document.Descendants(Wpf + "ScrollViewer"));
        Assert.Single(document.Descendants(Wpf + "Button"),
            button => (string?)button.Attribute("Command") == "{Binding ErstellenCommand}");
        Assert.Contains("Vollarchiv auf dem Server", DialogXaml, StringComparison.Ordinal);
        Assert.Contains("serverseitig festgelegt", DialogXaml, StringComparison.Ordinal);
        Assert.Contains("keine Wiederherstellung", DialogXaml, StringComparison.Ordinal);
        Assert.Contains("keinen Download", DialogXaml, StringComparison.Ordinal);
        Assert.Contains("keine Auswahl von Serverpfaden", DialogXaml, StringComparison.Ordinal);

        foreach (var path in new[] { "Ergebnis.Id", "Ergebnis.ErstelltUtc", "Ergebnis.Byteanzahl", "Ergebnis.InhaltHash" })
        {
            var binding = $"{{Binding {path}, Mode=OneWay}}";
            Assert.Contains(binding, DialogXaml, StringComparison.Ordinal);
        }

        var hashField = Assert.Single(document.Descendants(Wpf + "TextBox"));
        Assert.Equal("{StaticResource ApiHashFeld}", (string?)hashField.Attribute("Style"));
        Assert.Contains("Setter Property=\"IsReadOnly\" Value=\"True\"", Styles, StringComparison.Ordinal);
        Assert.Contains("Setter Property=\"TextWrapping\" Value=\"Wrap\"", Styles, StringComparison.Ordinal);
        Assert.DoesNotContain("Loaded=", DialogXaml, StringComparison.Ordinal);
        Assert.Contains("Binding Status, Mode=OneWay", DialogXaml, StringComparison.Ordinal);
        Assert.Contains("Binding VorgangsId, Mode=OneWay", DialogXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void DialogGuardsBusyCloseAndDisposesOwnedViewModelOnce()
    {
        Assert.Contains("if (!_disposed && _viewModel.IstBeschaeftigt)", DialogCode, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true;", DialogCode, StringComparison.Ordinal);
        var disposedIndex = DialogCode.IndexOf("_disposed = true;", StringComparison.Ordinal);
        var disposeViewModelIndex = DialogCode.IndexOf("_viewModel.Dispose();", StringComparison.Ordinal);
        Assert.True(disposedIndex >= 0 && disposeViewModelIndex > disposedIndex);
        Assert.Contains("Closing -= ClosingWindow;", DialogCode, StringComparison.Ordinal);
        Assert.Contains("Closed -= ClosedWindow;", DialogCode, StringComparison.Ordinal);
        Assert.Contains("if (_disposed) return;", DialogCode, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowRoutesExactlyOneExtrasMenuItemToGuardedHandler()
    {
        var document = XDocument.Parse(MainXaml);
        var extras = Assert.Single(document.Descendants(Wpf + "MenuItem"),
            item => (string?)item.Attribute("Header") == "Extras");
        Assert.Single(extras.Elements(Wpf + "MenuItem"),
            item => (string?)item.Attribute("Click") == "Sicherungsarchiv_Click");
        Assert.Contains("public event Func<Task>? SicherungsarchivAngefordert;", MainCode, StringComparison.Ordinal);

        var handler = Slice(MainCode, "private async void Sicherungsarchiv_Click", "private async void GarminImport_Click");
        Assert.Contains("_geschlossen", handler, StringComparison.Ordinal);
        Assert.Contains("_lifetime.IsCancellationRequested", handler, StringComparison.Ordinal);
        Assert.Contains("_verwaltungOffen", handler, StringComparison.Ordinal);
        Assert.Contains("_sicherungsarchivOffen", handler, StringComparison.Ordinal);
        Assert.Contains("await handler();", handler, StringComparison.Ordinal);
        Assert.Contains("Das Vollarchiv konnte nicht geöffnet werden.", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void CompositionIsLazyUsesBackendScopedJournalAndDisposesDialog()
    {
        var composition = Slice(Composition,
            "fenster.SicherungsarchivAngefordert += () =>",
            "fenster.ProfilBearbeitenAngefordert += async () =>");

        Assert.Contains("Environment.SpecialFolder.LocalApplicationData", composition, StringComparison.Ordinal);
        Assert.Contains("Path.IsPathFullyQualified(lokalerPfad)", composition, StringComparison.Ordinal);
        Assert.Contains("Path.Combine(Path.GetFullPath(lokalerPfad), \"AndyTours\", \"Sicherungsjournal\")", composition, StringComparison.Ordinal);
        Assert.Contains("new DateisystemSicherungsjournal(journalOrdner, uebergebeneSitzung.BasisAdresse)", composition, StringComparison.Ordinal);
        Assert.Contains("uebergebeneSitzung.SicherungsarchivErstellung", composition, StringComparison.Ordinal);
        Assert.Contains("fenster.LebenszeitToken", composition, StringComparison.Ordinal);
        Assert.Contains("new ApiSicherungsarchivFenster(vm) { Owner = fenster }", composition, StringComparison.Ordinal);
        Assert.Contains("dialog.ShowDialog();", composition, StringComparison.Ordinal);
        Assert.Contains("finally", composition, StringComparison.Ordinal);
        Assert.Contains("dialog.Dispose();", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.CreateDirectory", composition, StringComparison.Ordinal);
        Assert.DoesNotContain(".Oeffnen(", composition, StringComparison.Ordinal);
        Assert.DoesNotContain(".Erstellen(", composition, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchiveClientUsesTheExistingLongTimeoutSessionClient()
    {
        Assert.Contains("SicherungsarchivErstellung = new SicherungsarchivErstellungHttpClient(_importHttpClient);",
            Session, StringComparison.Ordinal);
        Assert.Contains("_importHttpClient = new HttpClient(_handler, disposeHandler: false)",
            Session, StringComparison.Ordinal);
        Assert.Contains("Timeout = TimeSpan.FromMinutes(10)", Session, StringComparison.Ordinal);
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
        using var stream = typeof(ApiSicherungsarchivUiVertragTests).Assembly.GetManifestResourceStream(logicalName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}
