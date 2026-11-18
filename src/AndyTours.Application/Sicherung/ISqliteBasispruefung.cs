using AndyTours.Contracts.V1;

namespace AndyTours.Application.Sicherung;

public interface ISqliteBasispruefung
{
    Task<SqliteBasispruefungAntwort> Pruefen(SqliteBasispruefungAnfrage anfrage, CancellationToken cancellationToken);
}
