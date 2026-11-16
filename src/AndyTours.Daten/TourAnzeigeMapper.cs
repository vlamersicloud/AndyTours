using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.Daten;

/// <summary>Überträgt ausschließlich die Core-Anzeigeprojektion in den optionalen Tourvertrag.</summary>
public static class TourAnzeigeMapper
{
    public static TourAnzeigeAntwort ToAntwort(Einheitszeile row)
    {
        if (row is null || row.Name is null || row.Aktivitaet is null || row.Kategorien is null || row.Equipment is null ||
            row.Trainingsbereich is null || row.Kommentar is null ||
            (row.DistanzKm is double distance && !double.IsFinite(distance)) ||
            (row.DurchschnittKmh is double average && !double.IsFinite(average)) ||
            (row.MaxKmh is double maxSpeed && !double.IsFinite(maxSpeed)))
            throw Invalid();

        return new TourAnzeigeAntwort(
            row.Datum,
            row.Startzeit,
            row.Name,
            row.Aktivitaet,
            row.Kategorien,
            row.Equipment,
            row.DistanzKm,
            row.Hoehenmeter,
            row.Fahrzeit is TimeSpan duration ? duration.Ticks / TimeSpan.TicksPerSecond : null,
            row.DurchschnittKmh,
            row.MaxKmh,
            row.HfDurchschnitt,
            row.HfMax,
            row.Trittfrequenz,
            row.Temperatur,
            row.MaxHoehe,
            row.Trainingsbereich,
            row.Kommentar,
            row.IstBestDistanz,
            row.IstBestHoehenmeter,
            row.IstBestFahrzeit,
            row.IstBestDurchschnittKmh,
            row.IstBestMaxKmh);
    }

    private static UngueltigeTourException Invalid() => new("Tour-Anzeigeprojektion ist ungültig.");
}
