using AndyTours.Core.Modell;
namespace AndyTours.Application.Export;

/// <summary>Serverinterner Routingkontext; darf weder serialisiert noch protokolliert werden.</summary>
public sealed record CourseExportServerkontext(Fahrprofil Profil, Routingeinstellungen Routing)
{
    public override string ToString() => "Server-Course-Kontext (vertraulich)";
}

/// <summary>Nur backendinterner Zugriff auf gespeicherte Course-Export-Einstellungen.</summary>
public interface ICourseExportServerdaten
{
    Task<CourseExportServerkontext> Laden(CancellationToken cancellationToken);
}
