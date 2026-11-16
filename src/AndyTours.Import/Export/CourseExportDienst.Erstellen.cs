using AndyTours.Application.Export;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using AndyTours.Import.Garmin;
namespace AndyTours.Import.Export;

public sealed partial class CourseExportDienst : ICourseExport
{
    public async Task<CourseExportAntwort> Erstellen(CourseExportAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (anfrage is null || string.IsNullOrWhiteSpace(anfrage.Name) || anfrage.Name.Trim().Length > 200 ||
            anfrage.Name.Any(char.IsControl) || anfrage.Verlauf is null || anfrage.Verlauf.Count is < 2 or > 2000000 ||
            !Enum.IsDefined(anfrage.Profil)) throw Invalid();
        try
        {
            var points = new List<Verlaufspunkt>(anfrage.Verlauf.Count); int gpsCount = 0;
            foreach (var p in anfrage.Verlauf)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (p is null || !double.IsFinite(p.DistanzKm) || p.DistanzKm < 0 || p.FahrzeitMillisekunden < 0 || p.FahrzeitMillisekunden > TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond ||
                    (p.Hoehe is double h && !double.IsFinite(h)) || (p.Geschwindigkeit is double speed && !double.IsFinite(speed)) ||
                    (p.Position is TourdetailPosition pos && (!double.IsFinite(pos.Breite) || !double.IsFinite(pos.Laenge) || pos.Breite is < -90 or > 90 || pos.Laenge is < -180 or > 180))) throw Invalid();
                Geoposition? position = p.Position is null ? null : new Geoposition(p.Position.Breite, p.Position.Laenge); if (position is not null) gpsCount++;
                points.Add(new Verlaufspunkt { ZeitpunktUtc = p.ZeitpunktUtc?.UtcDateTime, DistanzKm = p.DistanzKm, Fahrzeit = TimeSpan.FromTicks(checked(p.FahrzeitMillisekunden * TimeSpan.TicksPerMillisecond)), Hoehe = p.Hoehe, Puls = p.Puls, Geschwindigkeit = p.Geschwindigkeit, Trittfrequenz = p.Trittfrequenz, Temperatur = p.Temperatur, Position = position });
            }
            if (gpsCount < 2) throw Invalid();
            var context = await _serverdaten.Laden(cancellationToken).ConfigureAwait(false);
            var profile = ToCore(anfrage.Profil);
            var (turns, warning) = await LadeAbbiegungen(BildeCourse.Spur(points), context.Routing, profile, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var course = BildeCourse.AusVerlauf(points, anfrage.Name.Trim(), turns);
            var bytes = SchreibeFitCourse.Ausfuehren(course, _zeit.GetUtcNow().UtcDateTime, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return new(bytes, BildeCoursename.Dateiname(anfrage.Name.Trim()), warning);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException or IndexOutOfRangeException) { throw Invalid(); }
    }
}
