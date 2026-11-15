using System.Globalization;
using AndyTours.Core.Modell;

namespace AndyTours.Core.Berechnungen;

/// <summary>Bildet lückenlose Kalendersummen statt einer irreführenden Verbindung weit auseinanderliegender Jahre.</summary>
public static class BerechneAktivitaetsperioden
{
    /// <summary>Summiert nur Einheiten innerhalb beider einschließlich geltender Grenzen.</summary>
    public static IReadOnlyList<Aktivitaetsperiode> Ausfuehren(IEnumerable<Trainingseinheit> einheiten,
        DateOnly von, DateOnly bis, Zeitraster raster)
    {
        if (von > bis) { throw new ArgumentException("Das Startdatum muss vor oder auf dem Enddatum liegen."); }
        var gruppen = einheiten.Where(e => e.Datum >= von && e.Datum <= bis)
            .GroupBy(e => Beginn(e.Datum, raster)).ToDictionary(g => g.Key, g => g.ToList());
        var punkte = new List<Aktivitaetsperiode>();
        for (DateOnly start = Beginn(von, raster); ;)
        {
            if (punkte.Count >= 10000) { throw new ArgumentException("Zu viele Zeitabschnitte. Bitte Zeitraum verkürzen oder gröberes Raster wählen."); }
            DateOnly ende = Ende(start, raster);
            DateOnly links = start < von ? von : start;
            DateOnly rechts = ende > bis ? bis : ende;
            List<Trainingseinheit> daten = gruppen.GetValueOrDefault(start) ?? [];
            punkte.Add(new Aktivitaetsperiode(links, rechts, Beschriftung(start, raster), daten.Count,
                daten.Sum(e => e.DistanzKm), daten.Sum(e => (long)e.Hoehenmeter),
                TimeSpan.FromTicks(daten.Aggregate(0L, (sum, e) => checked(sum + e.Fahrzeit.Ticks)))));
            if (ende >= bis) { break; }
            start = ende.AddDays(1);
        }

        return punkte;
    }

    /// <summary>Kalenderbeginn; ISO-Wochen können im vorherigen Kalenderjahr beginnen.</summary>
    public static DateOnly Beginn(DateOnly datum, Zeitraster raster) => raster switch
    {
        Zeitraster.Jahr => new DateOnly(datum.Year, 1, 1),
        Zeitraster.Monat => new DateOnly(datum.Year, datum.Month, 1),
        Zeitraster.Woche => datum.DayNumber < ((int)datum.DayOfWeek + 6) % 7
            ? DateOnly.MinValue : datum.AddDays(-(((int)datum.DayOfWeek + 6) % 7)),
        _ => datum,
    };

    /// <summary>Ende ohne Überlauf am maximal darstellbaren Datum.</summary>
    private static DateOnly Ende(DateOnly start, Zeitraster raster) => raster switch
    {
        Zeitraster.Jahr => new DateOnly(start.Year, 12, 31),
        Zeitraster.Monat => new DateOnly(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month)),
        Zeitraster.Woche => start.DayNumber > DateOnly.MaxValue.DayNumber - 6 ? DateOnly.MaxValue : start.AddDays(6),
        _ => start,
    };

    /// <summary>Eindeutige Beschriftung einschließlich Jahr, insbesondere bei Kalenderwochen über Silvester.</summary>
    private static string Beschriftung(DateOnly start, Zeitraster raster) => raster switch
    {
        Zeitraster.Jahr => start.Year.ToString(CultureInfo.InvariantCulture),
        Zeitraster.Monat => start.ToString("MMM yyyy", CultureInfo.GetCultureInfo("de-DE")),
        Zeitraster.Woche => $"KW {ISOWeek.GetWeekOfYear(start.ToDateTime(TimeOnly.MinValue)):00}/{ISOWeek.GetYear(start.ToDateTime(TimeOnly.MinValue))}",
        _ => start.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
    };
}
