using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using AndyTours.Import.Garmin;

namespace AndyTours.Import.Planung;

public sealed class PlanungsCourseExportDienst : IPlanungsCourseExport
{
    private readonly TimeProvider _zeit;
    public PlanungsCourseExportDienst(TimeProvider? zeit = null) => _zeit = zeit ?? TimeProvider.System;

    public Task<CourseExportAntwort> Erstellen(PlanungsGpxExportAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (anfrage is null || anfrage.Planung is null) throw Invalid();
            string name = PlanungsDateiRegeln.NormalisiereName(anfrage.Name);
            Tourenplanung plan = PlanungsVertragMapper.ToCore(anfrage.Planung, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Course course = BildeCourse.AusPlanung(plan, name);
            byte[] bytes = SchreibeFitCourse.Ausfuehren(course, _zeit.GetUtcNow().UtcDateTime, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (bytes.Length is 0 || bytes.LongLength > PlanungsDateiRegeln.MaxDateiBytes) throw Invalid();
            return Task.FromResult(new CourseExportAntwort(bytes, BildeCoursename.Dateiname(name), null));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (StammdatenException) { throw; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException or FormatException)
        { throw Invalid(); }
    }

    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, "Planungs-Course-Export ungültig.");
}
