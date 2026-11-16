using AndyTours.Application.Export;
using Microsoft.Data.Sqlite;
namespace AndyTours.Daten;

public sealed class SqliteCourseExportServerdaten : ICourseExportServerdaten
{
    private readonly string _datenbankpfad;
    public SqliteCourseExportServerdaten(string datenbankpfad)
    {
        if (string.IsNullOrWhiteSpace(datenbankpfad) || !Path.IsPathFullyQualified(datenbankpfad)) throw new ArgumentException("Ein absoluter Datenbankpfad ist erforderlich.", nameof(datenbankpfad));
        _datenbankpfad = Path.GetFullPath(datenbankpfad);
    }

    public Task<CourseExportServerkontext> Laden(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var transaction = connection.BeginTransaction(deferred: true);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var planning = PlanungEinstellung.Laden(connection);
            cancellationToken.ThrowIfCancellationRequested();
            var routing = RoutingEinstellung.Laden(connection);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return Task.FromResult(new CourseExportServerkontext(planning.Profil, routing));
        }
        catch { try { transaction.Rollback(); } catch (SqliteException) { } throw; }
    }
}
