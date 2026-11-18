using System.Reflection;
using System.Text;

namespace AndyTours.Tests;

public sealed class ApiTourenFilterauswahlStrukturTests
{
    private static string Quelle(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Fact]
    public void ServerUsesRequiredFailClosedCapsAndDoesNotReturnPartialSnapshots()
    {
        var endpoint = Quelle("AndyTours.TourenFilterauswahlEndpunkt.cs");
        var limits = Quelle("AndyTours.TourenFilterauswahlGrenzen.cs");
        var serializer = Quelle("AndyTours.TourenFilterauswahlAntwortSerialisierer.cs");
        var storage = Quelle("AndyTours.SqliteTourenSpeicher.cs");
        var program = Quelle("AndyTours.Sicherung.Erstellung.Program.cs");
        var contract = Quelle("AndyTours.TourenFilterauswahlVertrag.cs");
        var client = Quelle("AndyTours.TourenHttpClient.cs");
        var selectionMethodStart = storage.IndexOf("AlleTreffer(", StringComparison.Ordinal);
        var listMethodStart = storage.IndexOf("public Task<TourListeAntwort> Liste(", selectionMethodStart, StringComparison.Ordinal);
        Assert.True(selectionMethodStart >= 0 && listMethodStart > selectionMethodStart);
        var selectionMethod = storage[selectionMethodStart..listMethodStart];
        Assert.Contains("Backend:Tourenauswahl:MaxTouren", endpoint, StringComparison.Ordinal);
        Assert.Contains("Backend:Tourenauswahl:MaxAntwortBytes", endpoint, StringComparison.Ordinal);
        Assert.Contains("TourenFilterauswahlGrenzen.TryParse", endpoint, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.DeserializeAsync<TourenFilterauswahlAnfrage>", endpoint, StringComparison.Ordinal);
        Assert.Contains("Status503ServiceUnavailable", endpoint, StringComparison.Ordinal);
        Assert.Contains("Status413PayloadTooLarge", endpoint, StringComparison.Ordinal);
        Assert.Contains("maxTouren > 0", limits, StringComparison.Ordinal);
        Assert.Contains("int.TryParse", limits, StringComparison.Ordinal);
        Assert.Contains("BegrenzterStream", serializer, StringComparison.Ordinal);
        Assert.Contains("maximum - _buffer.Length", serializer, StringComparison.Ordinal);
        Assert.Contains("BeginTransaction(deferred: true)", selectionMethod, StringComparison.Ordinal);
        Assert.Contains("Mode = SqliteOpenMode.ReadOnly", selectionMethod, StringComparison.Ordinal);
        Assert.Contains("Pooling = false", selectionMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("Oeffnen()", selectionMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("OeffneDatenbank", selectionMethod, StringComparison.Ordinal);
        Assert.Contains("TourenTabellenfilterMapper.ToFilter(filter.Tabellenfilter)", selectionMethod, StringComparison.Ordinal);
        Assert.Contains("FiltereEinheiten.Ausfuehren(alle, coreTableFilter, bestand)", selectionMethod, StringComparison.Ordinal);
        Assert.Contains("if (alle.Count > maximaleTouren)", selectionMethod, StringComparison.Ordinal);
        Assert.Contains("throw new TourenFilterauswahlLimitException()", selectionMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("Take(maximaleTouren)", storage, StringComparison.Ordinal);
        Assert.Contains("builder.Services.AddSingleton<ITourenFilterauswahl>", program, StringComparison.Ordinal);
        Assert.Contains("app.MapTourenFilterauswahl();", program, StringComparison.Ordinal);
        Assert.Contains("TourenFilterauswahlAnfrage", contract, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<TourAntwort> Touren", contract, StringComparison.Ordinal);
        Assert.Contains("auswahl/filtertreffer", client, StringComparison.Ordinal);
        Assert.Contains("SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)", client, StringComparison.Ordinal);
        Assert.Contains("RespectRequiredConstructorParameters = true", client, StringComparison.Ordinal);
        Assert.Contains("ct.ThrowIfCancellationRequested()", client, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientOnlyCommitsAnEchoedAppliedFilterAndACompleteImmutableCandidate()
    {
        var viewModel = Quelle("AndyTours.ApiUebersichtViewModel.Filterauswahl.cs");
        var selection = Quelle("AndyTours.ApiTourenauswahl.cs");
        var commit = Quelle("AndyTours.ApiTourenFilterauswahlCommit.cs");
        Assert.Contains("ApiTourenFilterauswahlCommit.TryCommit", viewModel, StringComparison.Ordinal);
        Assert.Contains("expectedGeneration != currentGeneration", commit, StringComparison.Ordinal);
        Assert.Contains("response.Filter != expectedFilter", commit, StringComparison.Ordinal);
        Assert.Contains("selection.SetzeAlle(response.Touren)", commit, StringComparison.Ordinal);
        Assert.Contains("catch (OperationCanceledException) when (ct.IsCancellationRequested)", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ListeGefiltert", viewModel, StringComparison.Ordinal);
        Assert.Contains("var candidate = new Dictionary<long, TourAntwort>()", selection, StringComparison.Ordinal);
        Assert.Contains("Pruefe(tour);", selection, StringComparison.Ordinal);
        Assert.Contains("_items = candidate;", selection, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkupRequiresExplicitClickAndExposesClearAndCount()
    {
        var markup = Quelle("AndyTours.ApiHauptfenster.xaml");
        var code = Quelle("AndyTours.ApiHauptfenster.ManuelleAuswahl.cs");
        Assert.Contains("Alle Filtertreffer auswählen", markup, StringComparison.Ordinal);
        Assert.Contains("AlleFiltertrefferAuswaehlen_Click", markup, StringComparison.Ordinal);
        Assert.Contains("KannAlleFiltertrefferAuswaehlen", markup, StringComparison.Ordinal);
        Assert.Contains("AuswahlAnzahl", markup, StringComparison.Ordinal);
        Assert.Contains("Auswahl aufheben", markup, StringComparison.Ordinal);
        Assert.Contains("_verwaltungOffen", code, StringComparison.Ordinal);
        Assert.Contains("token = _lifetime.Token", code, StringComparison.Ordinal);
        Assert.Contains("WaehleAlleFiltertrefferAsync(token)", code, StringComparison.Ordinal);
    }
}
