using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.App.ViewModels;

public static class ApiTourdetailMapper
{
    public static IReadOnlyList<Etappe> AlsEtappen(TourdetailAntwort antwort)
    {
        ArgumentNullException.ThrowIfNull(antwort);
        return antwort.Etappen.Select(stage => new Etappe(
            ApiFormularMapper.AlsEinheit(stage.Tour),
            stage.Verlauf is null ? null : stage.Verlauf.Select(point => new Verlaufspunkt
            {
                ZeitpunktUtc = point.ZeitpunktUtc?.UtcDateTime,
                DistanzKm = point.DistanzKm,
                Fahrzeit = TimeSpan.FromTicks(checked(point.FahrzeitMillisekunden * TimeSpan.TicksPerMillisecond)),
                Hoehe = point.Hoehe,
                Puls = point.Puls,
                Geschwindigkeit = point.Geschwindigkeit,
                Trittfrequenz = point.Trittfrequenz,
                Temperatur = point.Temperatur,
                Position = point.Position is null ? null : new Geoposition(point.Position.Breite, point.Position.Laenge)
            }).ToArray())).ToArray();
    }
}