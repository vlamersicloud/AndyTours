using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiTourSeitenUebernahmeVertragTests
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
    public void Table_filter_invalidates_only_table_and_selection_state()
    {
        var vm = Read("AndyTours.ApiUebersichtViewModel.cs");
        var method = Slice(vm, "private void TabellenfilterGeaendert", "private void FilterGeaendert");
        Assert.Contains("Interlocked.Increment(ref _generation)", method, StringComparison.Ordinal);
        Assert.Contains("Touren.Clear()", method, StringComparison.Ordinal);
        Assert.DoesNotContain("SetzeTourenauswahl(0)", method, StringComparison.Ordinal);
        Assert.DoesNotContain("HebeTourenauswahlAuf()", method, StringComparison.Ordinal);
        Assert.Contains("_start = 0", method, StringComparison.Ordinal);
        foreach (var untouched in new[] { "LeereAnzeige", "_perioden.Clear", "Jahresrekorde.Clear", "Einzelrekorde.Clear", "Jahresziele.Clear", "LeerePulsanzeige", "Diagramm =", "DistanzKmText =" })
            Assert.DoesNotContain(untouched, method, StringComparison.Ordinal);
    }

    [Fact]
    public void Tour_filter_bar_is_explicitly_applied_and_uses_per_bound_validation()
    {
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var document = XDocument.Parse(Read("AndyTours.ApiHauptfenster.xaml"));
        var tab = document.Descendants(p + "TabItem").Single(element => (string?)element.Attribute("Header") == "Touren");
        var elements = tab.Descendants().ToArray();
        Assert.Contains(elements, element => (string?)element.Attribute("Text") == "Nur Tourentabelle – Kennzahlen und Rekorde bleiben unverändert.");
        Assert.Contains(elements, element => (string?)element.Attribute("IsEnabled") == "{Binding FilterControlsEnabled}");
        Assert.Contains(elements, element => (string?)element.Attribute("Command") == "{Binding AktualisierenCommand}");
        Assert.Contains(elements, element => (string?)element.Attribute("Command") == "{Binding Tabellenfilter.ResetZuruecksetzenCommand}");

        var combos = elements.Where(element => element.Name == p + "ComboBox").ToArray();
        Assert.Equal(2, combos.Length);
        Assert.Contains(combos, element => (string?)element.Attribute("ItemsSource") == "{Binding Tabellenfilter.Equipments}" &&
            (string?)element.Attribute("SelectedItem") == "{Binding Tabellenfilter.AusgewaehltesEquipment, Mode=TwoWay}" &&
            (string?)element.Attribute("IsSynchronizedWithCurrentItem") == "False");
        Assert.Contains(combos, element => (string?)element.Attribute("ItemsSource") == "{Binding Tabellenfilter.Trainingsbereiche}" &&
            (string?)element.Attribute("SelectedItem") == "{Binding Tabellenfilter.AusgewaehlterBereich, Mode=TwoWay}" &&
            (string?)element.Attribute("IsSynchronizedWithCurrentItem") == "False");

        foreach (var field in new[] { "DistanzVon", "DistanzBis", "HoehenmeterVon", "HoehenmeterBis", "Suchbegriff" })
            Assert.Contains(elements, element => element.Name == p + "TextBox" &&
                ((string?)element.Attribute("Text"))?.Contains($"Tabellenfilter.{field}", StringComparison.Ordinal) == true &&
                ((string?)element.Attribute("Text"))?.Contains("UpdateSourceTrigger=PropertyChanged", StringComparison.Ordinal) == true);
        foreach (var flag in new[] { "DistanzVonFehlerhaft", "DistanzBisFehlerhaft", "HoehenmeterVonFehlerhaft", "HoehenmeterBisFehlerhaft" })
            Assert.Contains(elements, element => element.Name == p + "TextBox" &&
                (string?)element.Attribute("Tag") == $"{{Binding Tabellenfilter.{flag}}}" &&
                (string?)element.Attribute("Style") == "{StaticResource FilterRangeError}");

        var style = document.Descendants(p + "Style").Single(element => (string?)element.Attribute(x + "Key") == "FilterRangeError");
        Assert.Contains(style.Descendants(p + "Setter"), setter => (string?)setter.Attribute("Property") == "BorderBrush" &&
            (string?)setter.Attribute("Value") == "Red");
    }

    [Fact]
    public void Server_total_is_formatted_only_after_page_validation_and_bound_one_way()
    {
        var vm = Read("AndyTours.ApiUebersichtViewModel.cs");
        var refresh = Slice(vm, "public async Task AktualisierenAsync", "private async Task<ApiFilterOptionenPlan?> LadeFilterOptionenAsync");
        AssertOrder(refresh, "ApiTourSeitenRegeln.Validieren(page, 0, 50)", "ApiTourTabellensummeAnzeige.Erstellen(page)", "LeereAnzeige()");

        var paging = Slice(vm, "private async Task LadeSeiteAsync", "private void SetzeSeite");
        AssertOrder(paging, "ApiTourSeitenRegeln.Validieren(page, start, 50)", "ApiTourTabellensummeAnzeige.Erstellen(page)", "Tabellenfilter.Uebernehmen(tablePlan)");

        var sortSource = Read("AndyTours.ApiUebersichtViewModel.Sortierung.cs");
        var sorting = Slice(sortSource, "public async Task<bool> SortiereTourenAsync", "private static void ValidiereSortierantwort");
        AssertOrder(sorting, "ValidiereSortierantwort(page)", "ApiTourTabellensummeAnzeige.Erstellen(page)", "_tourenSortierung = schluessel");

        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var document = XDocument.Parse(Read("AndyTours.ApiHauptfenster.xaml"));
        var tab = document.Descendants(p + "TabItem").Single(element => (string?)element.Attribute("Header") == "Touren");
        Assert.Contains(tab.Descendants(p + "TextBlock"), element =>
            (string?)element.Attribute("Text") == "{Binding TabellensummeText, Mode=OneWay}");
    }

    private static void AssertOrder(string source, string first, string second, string third)
    {
        var firstIndex = source.IndexOf(first, StringComparison.Ordinal);
        var secondIndex = source.IndexOf(second, StringComparison.Ordinal);
        var thirdIndex = source.IndexOf(third, StringComparison.Ordinal);
        Assert.True(firstIndex >= 0 && secondIndex > firstIndex && thirdIndex > secondIndex);
    }

    [Fact]
    public void Refresh_prepares_candidates_and_validates_before_any_display_commit()
    {
        var vm = Read("AndyTours.ApiUebersichtViewModel.cs");
        var refresh = Slice(vm, "public async Task AktualisierenAsync", "private async Task<ApiFilterOptionenPlan?> LadeFilterOptionenAsync");
        Assert.Contains("var plan = await LadeFilterOptionenAsync", refresh, StringComparison.Ordinal);
        Assert.True(refresh.IndexOf("LadeFilterOptionenAsync", StringComparison.Ordinal) <
                    refresh.IndexOf("_auswertung.Laden", StringComparison.Ordinal));
        var awaitBoth = refresh.IndexOf("await Task.WhenAll(auswertungTask, tourenTask)", StringComparison.Ordinal);
        var postAwaitCt = refresh.IndexOf("ct.ThrowIfCancellationRequested()", awaitBoth, StringComparison.Ordinal);
        var postAwaitGeneration = refresh.IndexOf("generation != Interlocked.Read(ref _generation)", postAwaitCt, StringComparison.Ordinal);
        var validation = refresh.IndexOf("ApiTourSeitenRegeln.Validieren(page, 0, 50)", postAwaitGeneration, StringComparison.Ordinal);
        Assert.True(awaitBoth >= 0 && postAwaitCt > awaitBoth && postAwaitGeneration > postAwaitCt && validation > postAwaitGeneration);
        var validate = refresh.IndexOf("ApiTourSeitenRegeln.Validieren(page, 0, 50)", StringComparison.Ordinal);
        foreach (var commit in new[] { "LeereAnzeige()", "UebernehmeFilterOptionen(plan)", "_start = 0", "_perioden.Clear()", "SetzeSeite(page)", "Interlocked.Increment(ref _aktualisierungsstand)" })
            Assert.True(refresh.IndexOf(commit, StringComparison.Ordinal) > validate, $"Commit before validation: {commit}");
    }

    [Fact]
    public void Lookup_prepare_is_read_only_and_paging_validates_before_commit()
    {
        var vm = Read("AndyTours.ApiUebersichtViewModel.cs");
        var lookup = Slice(vm, "private async Task<ApiFilterOptionenPlan?> LadeFilterOptionenAsync", "private void UebernehmeFilterOptionen");
        Assert.DoesNotContain("LeereAnzeige", lookup, StringComparison.Ordinal);
        Assert.DoesNotContain("AktivitaetsOptionen.Clear", lookup, StringComparison.Ordinal);
        Assert.DoesNotContain("KategorieOptionen.Clear", lookup, StringComparison.Ordinal);
        Assert.DoesNotContain("_lookupsGeladen =", lookup, StringComparison.Ordinal);

        var paging = Slice(vm, "private async Task LadeSeiteAsync", "private void SetzeSeite");
        var filter = paging.IndexOf("var filter = new TourenListenfilter", StringComparison.Ordinal);
        var generation = paging.IndexOf("var generation = Interlocked.Read(ref _generation)", StringComparison.Ordinal);
        var busy = paging.IndexOf("Laedt = true", StringComparison.Ordinal);
        Assert.True(filter >= 0 && filter < busy && generation >= 0 && generation < busy);
        var awaitPage = paging.IndexOf("await _touren.ListeGefiltert", StringComparison.Ordinal);
        var ct = paging.IndexOf("ct.ThrowIfCancellationRequested()", awaitPage, StringComparison.Ordinal);
        var current = paging.IndexOf("generation != Interlocked.Read(ref _generation)", awaitPage, StringComparison.Ordinal);
        var validate = paging.IndexOf("ApiTourSeitenRegeln.Validieren(page, start, 50)", awaitPage, StringComparison.Ordinal);
        Assert.True(awaitPage >= 0 && ct > awaitPage && current > ct && validate > current);
        Assert.True(paging.IndexOf("_start = start", StringComparison.Ordinal) > validate);
        Assert.True(paging.IndexOf("SetzeSeite(page)", StringComparison.Ordinal) > validate);
    }

    [Fact]
    public void Sort_validates_before_state_commit_and_releases_busy_flag()
    {
        var source = Read("AndyTours.ApiUebersichtViewModel.Sortierung.cs");
        var method = Slice(source, "public async Task<bool> SortiereTourenAsync", "private static void ValidiereSortierantwort");
        var validate = method.IndexOf("ValidiereSortierantwort(page)", StringComparison.Ordinal);
        Assert.True(validate >= 0);
        foreach (var commit in new[] { "_tourenSortierung = schluessel", "_tourenAbsteigend = absteigend", "_start = 0", "SetzeSeite(page)" })
            Assert.True(method.IndexOf(commit, StringComparison.Ordinal) > validate, $"Sort commit before validation: {commit}");
        Assert.Contains("finally", method, StringComparison.Ordinal);
        Assert.Contains("Laedt = false", method, StringComparison.Ordinal);
        var wrapper = source[(source.IndexOf("private static void ValidiereSortierantwort", StringComparison.Ordinal))..];
        Assert.Contains("ApiTourSeitenRegeln.Validieren(page, 0, 50)", wrapper, StringComparison.Ordinal);
    }
}