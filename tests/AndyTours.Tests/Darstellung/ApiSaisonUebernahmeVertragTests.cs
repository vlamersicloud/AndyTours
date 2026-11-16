using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiSaisonUebernahmeVertragTests
{
    private static string Read(string resource)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Slice(string source, string start, string end)
    {
        var first = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(first >= 0, $"Missing source marker: {start}");
        var last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
        Assert.True(last > first, $"Missing end marker: {end}");
        return source[first..last];
    }

    [Fact]
    public void Season_projection_is_prepared_after_validation_and_before_refresh_commit()
    {
        var vm = Read("AndyTours.ApiUebersichtViewModel.cs");
        var refresh = Slice(vm, "public async Task AktualisierenAsync", "private async Task<ApiFilterOptionenPlan?> LadeFilterOptionenAsync");
        AssertOrder(refresh, "ApiTourSeitenRegeln.Validieren(page, 0, 50)", "ApiTourTabellensummeAnzeige.Erstellen(page)",
            "ApiSaisonAnzeige.Erstellen(result.Saisonfortschritt, von, bis)", "LeereAnzeige()");
        AssertOrder(refresh, "ApiSaisonAnzeige.Erstellen(result.Saisonfortschritt, von, bis)",
            "UebernehmeFilterOptionen(plan)", "_perioden.Clear()", "Interlocked.Increment(ref _aktualisierungsstand)");
        var customCatch = refresh.IndexOf("catch (ApiSaisonAntwortException)", StringComparison.Ordinal);
        Assert.True(customCatch >= 0);
        var guardedCatch = refresh[customCatch..];
        Assert.Contains("generation == Interlocked.Read(ref _generation)", guardedCatch, StringComparison.Ordinal);
        Assert.Contains("!ct.IsCancellationRequested", guardedCatch, StringComparison.Ordinal);
        Assert.Contains("StatusText = \"Saisonkennzahlen sind unvollständig oder ungültig.\"", guardedCatch, StringComparison.Ordinal);
    }

    [Fact]
    public void Global_filter_reset_clears_season_but_table_filter_preserves_it()
    {
        var vm = Read("AndyTours.ApiUebersichtViewModel.cs");
        var clear = Slice(vm, "private void LeereAnzeige()", "[RelayCommand(CanExecute = nameof(KannAktualisieren))]");
        Assert.Contains("_saisonTexte = ApiSaisonTexte.Leer", clear, StringComparison.Ordinal);
        Assert.Contains("SaisonText = string.Empty", clear, StringComparison.Ordinal);
        var table = Slice(vm, "private void TabellenfilterGeaendert()", "private void FilterGeaendert()");
        Assert.DoesNotContain("_saisonTexte", table, StringComparison.Ordinal);
        Assert.DoesNotContain("SaisonText", table, StringComparison.Ordinal);
    }

    [Fact]
    public void Metric_selection_uses_prepared_text_without_network_or_recalculation()
    {
        var vm = Read("AndyTours.ApiUebersichtViewModel.cs");
        var selector = Slice(vm, "private void AktualisiereSaisonText()", "partial void OnVonDatumChanged");
        foreach (var mapping in new[]
        {
            "ApiDiagrammkennzahl.Kilometer => _saisonTexte.Kilometer",
            "ApiDiagrammkennzahl.Hoehenmeter => _saisonTexte.Hoehenmeter",
            "ApiDiagrammkennzahl.Anzahl => _saisonTexte.Anzahl",
            "ApiDiagrammkennzahl.Fahrzeit => _saisonTexte.Fahrzeit"
        }) Assert.Contains(mapping, selector, StringComparison.Ordinal);
        var changed = Slice(vm, "partial void OnKennzahlChanged", "partial void OnVonDatumChanged");
        Assert.Contains("AktualisiereSaisonText()", changed, StringComparison.Ordinal);
        Assert.DoesNotContain("_auswertung.Laden", changed, StringComparison.Ordinal);
        Assert.DoesNotContain("Datenbestand", changed, StringComparison.Ordinal);
    }

    [Fact]
    public void Season_summary_is_a_separate_readonly_overview_row()
    {
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var document = XDocument.Parse(Read("AndyTours.ApiHauptfenster.xaml"));
        var overview = document.Descendants(p + "TabItem").Single(element => (string?)element.Attribute("Header") == "Übersicht");
        var text = Assert.Single(overview.Descendants(p + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding SaisonText, Mode=OneWay}");
        Assert.Equal("Wrap", (string?)text.Attribute("TextWrapping"));
        Assert.Equal("1", (string?)text.Attribute("Grid.Row"));
        var plot = Assert.Single(overview.Descendants(), element => element.Name.LocalName == "PlotView");
        Assert.Equal("0", (string?)plot.Attribute("Grid.Row") ?? "0");
        Assert.NotEqual((string?)plot.Attribute("Grid.Row") ?? "0", (string?)text.Attribute("Grid.Row"));
        Assert.Contains(overview.Descendants(p + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding ZukunftsHinweis}" && (string?)element.Attribute("Grid.Row") == "2");
    }

    private static void AssertOrder(string source, params string[] markers)
    {
        var previous = -1;
        foreach (var marker in markers)
        {
            var index = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index > previous, $"Unexpected order or missing marker: {marker}");
            previous = index;
        }
    }
}
