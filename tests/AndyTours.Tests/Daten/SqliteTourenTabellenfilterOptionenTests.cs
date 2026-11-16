using System.Text.Json;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed partial class SqliteTourenAnzeigeTests
{
    private static readonly string[] ExpectedOptionEquipments = ["Bike", "Retired (ausgemustert)"];
    private static readonly string[] ExpectedOptionAreas = ["GA1", "GA2"];
    private static readonly string[] ExpectedRetiredOnly = ["Retired (ausgemustert)"];
    private static readonly string[] ExpectedWithMissingEquipment = ["Bike", "Missing", "Retired (ausgemustert)"];
    private static readonly string[] ExpectedWithMissingArea = ["Absent", "GA1", "GA2"];

    [Fact]
    public async Task Filter_options_use_date_activity_base_not_category_table_filter_or_page_and_preserve_data()
    {
        using var connection = OeffneDatenbank.Ausfuehren(_database);
        var rawBefore = JsonSerializer.Serialize(TrainingseinheitenTabelle.Laden(connection));
        var firstTour = TrainingseinheitenTabelle.Laden(connection).Single(row => row.Id == _ids[0]);
        var activityId = firstTour.AktivitaetId;
        var category = firstTour.KategorieIds[0];

        var baseline = await _store.ListeGefiltert(1, 1, new(_from, _to, null, null));
        Assert.Equal(3, baseline.Gesamt);
        Assert.Equal(ExpectedOptionEquipments, baseline.TabellenfilterOptionen!.Equipments);
        Assert.Equal(ExpectedOptionAreas, baseline.TabellenfilterOptionen.Trainingsbereiche);

        var categoryNarrowed = await _store.ListeGefiltert(0, 1, new(_from, _to, null, category));
        Assert.Equal(1, categoryNarrowed.Gesamt);
        Assert.Equal(ExpectedOptionEquipments, categoryNarrowed.TabellenfilterOptionen!.Equipments);
        Assert.Equal(ExpectedOptionAreas, categoryNarrowed.TabellenfilterOptionen.Trainingsbereiche);

        var activityPage = await _store.ListeGefiltert(99, 1, new(_from, _to, activityId, null));
        Assert.Empty(activityPage.Eintraege);
        Assert.Equal(2, activityPage.Gesamt);
        Assert.Equal(ExpectedRetiredOnly, activityPage.TabellenfilterOptionen!.Equipments);

        var missing = await _store.ListeGefiltert(0, 1, new(_from, _to, null, null)
        {
            Tabellenfilter = new TourenTabellenfilter(EquipmentName: "Missing", Trainingsbereich: "Absent")
        });
        Assert.Equal(0, missing.Gesamt);
        Assert.Equal(ExpectedWithMissingEquipment, missing.TabellenfilterOptionen!.Equipments);
        Assert.Equal(ExpectedWithMissingArea, missing.TabellenfilterOptionen.Trainingsbereiche);

        var emptyBase = await _store.ListeGefiltert(0, 1, new(new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 31), null, null));
        Assert.Empty(emptyBase.TabellenfilterOptionen!.Equipments);
        Assert.Empty(emptyBase.TabellenfilterOptionen.Trainingsbereiche);
        Assert.Equal(rawBefore, JsonSerializer.Serialize(TrainingseinheitenTabelle.Laden(connection)));
    }
}
