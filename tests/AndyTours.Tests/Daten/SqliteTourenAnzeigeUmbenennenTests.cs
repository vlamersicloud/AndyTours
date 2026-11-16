using System.Collections.Generic;
using System.Text.Json;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed partial class SqliteTourenAnzeigeTests
{
    [Fact]
    public async Task Renamed_lookup_labels_change_projection_and_sort_without_changing_tour_identity_or_values()
    {
        var beforePages = new List<TourAntwort>();
        for (var start = 0; start < 3; start++)
            beforePages.Add(Assert.Single((await _store.ListeGefiltert(start, 1, new(_from, _to, null, null)
                { Sortierung = TourenSortierschluessel.Bestand })).Eintraege));
        var beforeById = beforePages.ToDictionary(x => x.Id);
        using var connection = OeffneDatenbank.Ausfuehren(_database);
        var rawBefore = JsonSerializer.Serialize(TrainingseinheitenTabelle.Laden(connection));

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE Aktivitaet SET Name = @name WHERE Id = @id";
            command.Parameters.AddWithValue("@name", "AAA");
            command.Parameters.AddWithValue("@id", _ids[0] == 0 ? -1 : TrainingseinheitenTabelle.Laden(connection).Single(t => t.Id == _ids[0]).AktivitaetId);
            Assert.Equal(1, command.ExecuteNonQuery());
        }
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE Equipment SET Name = @name WHERE Id = @id";
            command.Parameters.AddWithValue("@name", "ZZZ");
            command.Parameters.AddWithValue("@id", TrainingseinheitenTabelle.Laden(connection).Single(t => t.Id == _ids[1]).EquipmentId!.Value);
            Assert.Equal(1, command.ExecuteNonQuery());
        }
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE Kategorie SET Name = @name WHERE Name = @nameOld";
            command.Parameters.AddWithValue("@name", "Zebra");
            command.Parameters.AddWithValue("@nameOld", "Alpin");
            Assert.Equal(1, command.ExecuteNonQuery());
        }

        var after = new List<TourAntwort>();
        for (var start = 0; start < 3; start++)
            after.Add(Assert.Single((await _store.ListeGefiltert(start, 1, new(_from, _to, null, null)
                { Sortierung = TourenSortierschluessel.Bestand })).Eintraege));
        var afterById = after.ToDictionary(x => x.Id);

        foreach (long id in _ids)
        {
            Assert.Equal(beforeById[id].Version, afterById[id].Version);
            Assert.Equal(JsonSerializer.Serialize(beforeById[id].Werte), JsonSerializer.Serialize(afterById[id].Werte));
        }
        Assert.Equal("AAA", afterById[_ids[0]].Anzeige!.Aktivitaet);
        Assert.Equal("AAA", afterById[_ids[2]].Anzeige!.Aktivitaet);
        Assert.Equal("ZZZ", afterById[_ids[1]].Anzeige!.Equipment);
        Assert.Equal("Zebra, Zulu", afterById[_ids[0]].Anzeige!.Kategorien);

        var activityOrder = (await _store.ListeGefiltert(0, 3, new(_from, _to, null, null)
            { Sortierung = TourenSortierschluessel.Aktivitaet })).Eintraege.Select(x => x.Id);
        Assert.Equal(new[] { _ids[0], _ids[2], _ids[1] }, activityOrder);
        var equipmentOrder = (await _store.ListeGefiltert(0, 3, new(_from, _to, null, null)
            { Sortierung = TourenSortierschluessel.Equipment })).Eintraege.Select(x => x.Id);
        Assert.Equal(new[] { _ids[2], _ids[0], _ids[1] }, equipmentOrder);
        Assert.Equal(rawBefore, JsonSerializer.Serialize(TrainingseinheitenTabelle.Laden(connection)));
    }
}