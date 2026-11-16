using AndyTours.Contracts.V1;
using AndyTours.Application.Export;

namespace AndyTours.Application.Planung;

public interface IPlanungsCourseExport
{
    Task<CourseExportAntwort> Erstellen(PlanungsGpxExportAnfrage anfrage, CancellationToken cancellationToken);
}
