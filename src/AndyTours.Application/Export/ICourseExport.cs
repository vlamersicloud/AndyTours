using AndyTours.Contracts.V1;
namespace AndyTours.Application.Export;

public interface ICourseExport
{
    Task<CourseExportEinstellungenAntwort> LadenEinstellungen(CancellationToken cancellationToken);
    Task<CourseExportAntwort> Erstellen(CourseExportAnfrage anfrage, CancellationToken cancellationToken);
}
