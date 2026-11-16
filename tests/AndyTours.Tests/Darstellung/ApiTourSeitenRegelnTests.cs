using System.Text.Json;
using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiTourSeitenRegelnTests
{
    private const string SafeMessage = "Tourenseite ist unvollständig oder ungültig. Bitte Backend-Version und Verbindung prüfen.";

    [Fact]
    public void Accepts_valid_empty_past_end_and_int_max_metadata()
    {
        ApiTourSeitenRegeln.Validieren(Page([], 0, 10), 0, 10);
        ApiTourSeitenRegeln.Validieren(Page([], 10, 10), 10, 10);
        ApiTourSeitenRegeln.Validieren(Page([], int.MaxValue, int.MaxValue), int.MaxValue, int.MaxValue);
        ApiTourSeitenRegeln.Validieren(Page([Tour()], 0, 10), 0, 10);
    }

    [Fact]
    public void Rejects_invalid_page_structure_with_fixed_message()
    {
        var good = Tour();
        AssertBad(null, 0, 10);
        AssertBad(new TourListeAntwort(null!, 0, 0, 10), 0, 10);
        AssertBad(new TourListeAntwort(new TourAntwort[] { null! }, 1, 0, 10), 0, 10);
        AssertBad(Page([], -1, 10), 0, 10);
        AssertBad(Page([], 0, -1), 0, 10);
        AssertBad(Page([], 0, 0), 0, 10);
        AssertBad(Page([], 1, 10), 0, 10);
        AssertBad(Page([good], 0, 10, total: 0), 0, 10);
        AssertBad(Page([good, good with { }], 0, 10, total: 2), 0, 10);
        AssertBad(Page([good with { Id = 0 }], 0, 10), 0, 10);
        AssertBad(Page([good with { Version = "bad" }], 0, 10), 0, 10);
        AssertBad(Page([good with { Werte = null! }], 0, 10), 0, 10);
        AssertBad(Page([good with { Werte = good.Werte with { KategorieIds = null! } }], 0, 10), 0, 10);
        AssertBad(Page([good with { Anzeige = null }], 0, 10), 0, 10);
    }

    [Fact]
    public void Preserves_nullable_zero_negative_temperature_and_does_not_mutate_source()
    {
        var row = Tour();
        var page = Page([row], 0, 10);
        var before = JsonSerializer.Serialize(page);
        ApiTourSeitenRegeln.Validieren(page, 0, 10);
        Assert.Equal(before, JsonSerializer.Serialize(page));
        Assert.Equal(0d, row.Anzeige!.DistanzKm);
        Assert.Null(row.Anzeige.DurchschnittKmh);
        Assert.Equal(-5, row.Anzeige.Temperatur);
    }

    [Fact]
    public void Rejects_missing_strings_nonfinite_values_and_each_metadata_mismatch()
    {
        var row = Tour();
        var a = row.Anzeige!;
        AssertBadRow(row with { Anzeige = a with { Name = null! } });
        AssertBadRow(row with { Anzeige = a with { Aktivitaet = null! } });
        AssertBadRow(row with { Anzeige = a with { Kategorien = null! } });
        AssertBadRow(row with { Anzeige = a with { Equipment = null! } });
        AssertBadRow(row with { Anzeige = a with { Trainingsbereich = null! } });
        AssertBadRow(row with { Anzeige = a with { Kommentar = null! } });
        AssertBadRow(row with { Anzeige = a with { DistanzKm = double.NaN } });
        AssertBadRow(row with { Anzeige = a with { DurchschnittKmh = double.PositiveInfinity } });
        AssertBadRow(row with { Anzeige = a with { MaxKmh = double.NegativeInfinity } });
        AssertBadRow(row with { Anzeige = a with { Datum = a.Datum.AddDays(1) } });
        AssertBadRow(row with { Anzeige = a with { Startzeit = new TimeOnly(1, 2) } });
        AssertBadRow(row with { Anzeige = a with { Name = "different" } });
        AssertBadRow(row with { Anzeige = a with { Hoehenmeter = a.Hoehenmeter + 1 } });
        AssertBadRow(row with { Anzeige = a with { Trainingsbereich = "different" } });
        AssertBadRow(row with { Anzeige = a with { Kommentar = "different" } });
    }

    private static void AssertBadRow(TourAntwort row) => AssertBad(Page([row], 0, 10), 0, 10);

    private static void AssertBad(TourListeAntwort? page, int start, int amount)
    {
        var ex = Assert.Throws<ApiTourSeitenException>(() => ApiTourSeitenRegeln.Validieren(page, start, amount));
        Assert.Equal(SafeMessage, ex.Message);
    }

    private static TourListeAntwort Page(IReadOnlyList<TourAntwort> rows, int start, int amount, int? total = null) =>
        new(rows, total ?? Math.Max(rows.Count, start + rows.Count), start, amount);

    private static TourAntwort Tour()
    {
        var values = new TourWerte(1, new DateOnly(2026, 1, 2), null, "Tour", 60, 0, 0, 0, 0,
            0, 100, 120, 80, 0, -5, 0, 0, 0, "GA", "", 1, null, []);
        var display = new TourAnzeigeAntwort(values.Datum, values.Startzeit, values.Name, "Rad", "", "",
            0, 0, null, null, null, null, null, null, -5, 0, values.Trainingsbereich, values.Kommentar,
            false, false, false, false, false);
        return new TourAntwort(1, new string('a', 64), values) { Anzeige = display };
    }
}