using System.Reflection;
using System.Text;

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