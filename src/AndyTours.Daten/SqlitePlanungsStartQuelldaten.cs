using AndyTours.Application.Planung;
using AndyTours.Core.Berechnungen;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Server-internal list of stored relative FIT paths for planning start-area lookup.</summary>
public sealed class SqlitePlanungsStartQuelldaten : IPlanungsStartQuelldaten
{
    private readonly string _datenbankpfad;

    public SqlitePlanungsStartQuelldaten(string datenbankpfad)
    {
        if (string.IsNullOrWhiteSpace(datenbankpfad) || !Path.IsPathFullyQualified(datenbankpfad))
            throw new ArgumentException("Ein absoluter Datenbankpfad ist erforderlich.", nameof(datenbankpfad));
        _datenbankpfad = Path.GetFullPath(datenbankpfad);
    }

    public Task<IReadOnlyList<string>> Laden(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var transaction = connection.BeginTransaction(deferred: true);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var einheiten = TrainingseinheitenTabelle.Laden(connection)
                .OrderByDescending(einheit => einheit.Datum)
                .ThenByDescending(einheit => einheit.Startzeit)
                .ToArray();
            var pfade = new List<string>();
            foreach (var einheit in einheiten)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pfad = FindeAufzeichnung.Ausfuehren(einheit);
                if (pfad is not null) pfade.Add(pfad);
            }
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return Task.FromResult<IReadOnlyList<string>>(Array.AsReadOnly(pfade.ToArray()));
        }
        catch
        {
            try { transaction.Rollback(); } catch (SqliteException) { }
            throw;
        }
    }
}
