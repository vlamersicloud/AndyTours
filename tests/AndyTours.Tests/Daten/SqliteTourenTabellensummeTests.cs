using System.Text.Json;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed partial class SqliteTourenAnzeigeTests
{
    [Fact]
    public async Task Table_sum_covers_all_filtered_tours_independent_of_page_and_preserves_storage()
    {
        using var connection = OeffneDatenbank.Ausfuehren(_database);
        var rawBefore = JsonSerializer.Serialize(TrainingseinheitenTabelle.Laden(connection));
        var originals = TrainingseinheitenTabelle.Laden(connection).ToDictionary(row => row.Id, TourenMapper.ToAntwort);
        var filter = new TourenListenfilter(_from, _to, null, null);

        var first = await _store.ListeGefiltert(0, 1, filter);
        var second = await _store.ListeGefiltert(1, 1, filter);
        var pastEnd = await _store.ListeGefiltert(10, 1, filter);
        var firstTour = Assert.Single(first.Eintraege);
        var secondTour = Assert.Single(second.Eintraege);
        Assert.NotEqual(firstTour.Id, secondTour.Id);
        Assert.Empty(pastEnd.Eintraege);
        Assert.Equal(new TourenTabellensummeAntwort(3, 120, 300, 10800), first.Tabellensumme);
        Assert.Equal(first.Tabellensumme, second.Tabellensumme);
        Assert.Equal(first.Tabellensumme, pastEnd.Tabellensumme);

        var ga1 = await _store.ListeGefiltert(0, 1, filter with { Tabellenfilter = new TourenTabellenfilter(Trainingsbereich: "GA1") });
        Assert.Equal(new TourenTabellensummeAntwort(2, 120, 300, 10800), ga1.Tabellensumme);
        var distance = await _store.ListeGefiltert(0, 1, filter with { Tabellenfilter = new TourenTabellenfilter(DistanzVon: "50") });
        Assert.Equal(new TourenTabellensummeAntwort(1, 100, 200, 7200), distance.Tabellensumme);
        var none = await _store.ListeGefiltert(0, 1, filter with { Tabellenfilter = new TourenTabellenfilter(Suchbegriff: "no match") });
        Assert.Equal(new TourenTabellensummeAntwort(0, 0, 0, 0), none.Tabellensumme);

        foreach (var response in first.Eintraege.Concat(second.Eintraege).Concat(ga1.Eintraege).Concat(distance.Eintraege))
        {
            var original = originals[response.Id];
            Assert.Equal(original.Id, response.Id);
            Assert.Equal(original.Version, response.Version);
            Assert.Equal(JsonSerializer.Serialize(original.Werte), JsonSerializer.Serialize(response.Werte));
        }
        Assert.Equal(rawBefore, JsonSerializer.Serialize(TrainingseinheitenTabelle.Laden(connection)));
    }
}
