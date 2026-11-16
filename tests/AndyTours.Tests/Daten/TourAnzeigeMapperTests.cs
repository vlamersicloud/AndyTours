using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;

namespace AndyTours.Tests.Daten;

public sealed class TourAnzeigeMapperTests
{
    [Fact]
    public void Maps_all_core_display_fields_without_normalizing_values()
    {
        var source = new Einheitszeile
        {
            Id = 1234,
            Datum = new DateOnly(2025, 4, 3),
            Startzeit = new TimeOnly(7, 8),
            Name = "Tour",
            Aktivitaet = "Rad",
            Kategorien = "Alpin, Lang",
            Equipment = "Rad (ausgemustert)",
            DistanzKm = 0,
            Hoehenmeter = 0,
            Fahrzeit = TimeSpan.FromTicks(1234567890123),
            DurchschnittKmh = null,
            MaxKmh = 45.75,
            HfDurchschnitt = null,
            HfMax = 0,
            Trittfrequenz = 90,
            Temperatur = -7,
            MaxHoehe = null,
            Trainingsbereich = "GA2",
            Kommentar = "Notiz",
            IstBestDistanz = true,
            IstBestHoehenmeter = false,
            IstBestFahrzeit = true,
            IstBestDurchschnittKmh = false,
            IstBestMaxKmh = true,
        };
        var mapped = TourAnzeigeMapper.ToAntwort(source);

        Assert.Equal(new DateOnly(2025, 4, 3), mapped.Datum);
        Assert.Equal(new TimeOnly(7, 8), mapped.Startzeit);
        Assert.Equal("Tour", mapped.Name);
        Assert.Equal("Rad", mapped.Aktivitaet);
        Assert.Equal("Alpin, Lang", mapped.Kategorien);
        Assert.Equal("Rad (ausgemustert)", mapped.Equipment);
        Assert.Equal(0, mapped.DistanzKm);
        Assert.Equal(0, mapped.Hoehenmeter);
        Assert.Equal(123456L, mapped.FahrzeitSekunden);
        Assert.Null(mapped.DurchschnittKmh);
        Assert.Equal(45.75, mapped.MaxKmh);
        Assert.Null(mapped.HfDurchschnitt);
        Assert.Equal(0, mapped.HfMax);
        Assert.Equal(90, mapped.Trittfrequenz);
        Assert.Equal(-7, mapped.Temperatur);
        Assert.Null(mapped.MaxHoehe);
        Assert.Equal("GA2", mapped.Trainingsbereich);
        Assert.Equal("Notiz", mapped.Kommentar);
        Assert.Equal((true, false, true, false, true),
            (mapped.IstBestDistanz, mapped.IstBestHoehenmeter, mapped.IstBestFahrzeit, mapped.IstBestDurchschnittKmh, mapped.IstBestMaxKmh));
        Assert.Equal(1234, source.Id);
    }

    [Fact]
    public void Null_start_and_duration_stay_null_and_invalid_projection_uses_fixed_safe_error()
    {
        var source = new Einheitszeile { Startzeit = null, Fahrzeit = null };
        var mapped = TourAnzeigeMapper.ToAntwort(source);
        Assert.Null(mapped.Startzeit);
        Assert.Null(mapped.FahrzeitSekunden);

        var error = Assert.Throws<UngueltigeTourException>(() => TourAnzeigeMapper.ToAntwort(new Einheitszeile { DistanzKm = double.NaN }));
        Assert.Equal("Tour-Anzeigeprojektion ist ungültig.", error.Message);
    }
}
