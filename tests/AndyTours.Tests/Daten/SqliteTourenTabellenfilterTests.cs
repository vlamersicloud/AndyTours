using System.Text.Json;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed partial class SqliteTourenAnzeigeTests
{
    [Fact]
    public async Task Table_filters_use_core_rules_before_sort_paging_and_preserve_tour_values()
    {
        using var connection = OeffneDatenbank.Ausfuehren(_database);
        var rawBefore = JsonSerializer.Serialize(TrainingseinheitenTabelle.Laden(connection));
        var originals = TrainingseinheitenTabelle.Laden(connection).ToDictionary(x => x.Id, TourenMapper.ToAntwort);

        async Task<TourListeAntwort> Page(TourenTabellenfilter tableFilter, int start, int amount)
        {
            var response = await _store.ListeGefiltert(start, amount,
                new(_from, _to, null, null)
                {
                    Tabellenfilter = tableFilter,
                    Sortierung = TourenSortierschluessel.Name
                });
            Assert.Equal(start, response.Start);
            return response;
        }

        async Task<long[]> Ids(TourenTabellenfilter tableFilter, int expectedCount, params long[] expectedIds)
        {
            var response = await Page(tableFilter, 0, expectedIds.Length);
            Assert.Equal(expectedCount, response.Gesamt);
            var rows = response.Eintraege.ToArray();
            Assert.Equal(expectedIds, rows.Select(x => x.Id));
            foreach (var result in rows)
            {
                var original = originals[result.Id];
                Assert.Equal(original.Id, result.Id);
                Assert.Equal(original.Version, result.Version);
                Assert.Equal(JsonSerializer.Serialize(original.Werte), JsonSerializer.Serialize(result.Werte));
            }
            return rows.Select(x => x.Id).ToArray();
        }

        await Ids(new TourenTabellenfilter(Trainingsbereich: "GA1"), 2, _ids[1], _ids[2]);
        await Ids(new TourenTabellenfilter(DistanzVon: "20", DistanzBis: "100"), 2, _ids[1], _ids[2]);
        await Ids(new TourenTabellenfilter(DistanzVon: "0", DistanzBis: "20"), 1, _ids[1]);
        await Ids(new TourenTabellenfilter(HoehenmeterVon: "0", HoehenmeterBis: "0"), 1, _ids[0]);
        await Ids(new TourenTabellenfilter(Suchbegriff: "b a"), 1, _ids[2]);
        await Ids(new TourenTabellenfilter(DistanzVon: "invalid"), 3, _ids[1], _ids[2], _ids[0]);
        await Ids(new TourenTabellenfilter(DistanzVon: "20", DistanzBis: "10"), 3, _ids[1], _ids[2], _ids[0]);
        await Ids(new TourenTabellenfilter(EquipmentName: "Retired (ausgemustert)"), 1, _ids[0]);
        await Ids(new TourenTabellenfilter(EquipmentName: "Bike (ausgemustert)"), 1, _ids[1]);

        var firstGaPage = await Page(new TourenTabellenfilter(Trainingsbereich: "GA1"), 0, 1);
        var secondGaPage = await Page(new TourenTabellenfilter(Trainingsbereich: "GA1"), 1, 1);
        Assert.Equal(2, firstGaPage.Gesamt);
        Assert.Equal(2, secondGaPage.Gesamt);
        Assert.Equal(_ids[1], Assert.Single(firstGaPage.Eintraege).Id);
        Assert.False(firstGaPage.Eintraege[0].Anzeige!.IstBestDistanz);
        Assert.Equal(_ids[2], Assert.Single(secondGaPage.Eintraege).Id);
        Assert.True(secondGaPage.Eintraege[0].Anzeige!.IstBestDistanz);

        var narrowed = await _store.ListeGefiltert(0, 1,
            new(_from, _to, null, null)
            {
                Tabellenfilter = new TourenTabellenfilter(DistanzBis: "20"),
                Sortierung = TourenSortierschluessel.Name
            });
        var best = Assert.Single(narrowed.Eintraege);
        Assert.Equal(_ids[1], best.Id);
        Assert.True(best.Anzeige!.IstBestDistanz);
        Assert.True(best.Anzeige.IstBestHoehenmeter);
        Assert.Equal(1, narrowed.Gesamt);

        Assert.Equal(rawBefore, JsonSerializer.Serialize(TrainingseinheitenTabelle.Laden(connection)));
    }
}