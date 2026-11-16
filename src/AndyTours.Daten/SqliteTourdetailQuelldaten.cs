using AndyTours.Application.Auswertung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Loads tour detail source metadata from one consistent server-side SQLite snapshot.</summary>
public sealed class SqliteTourdetailQuelldaten(string datenbankpfad) : ITourdetailQuelldaten
{
    public Task<TourdetailQuellbestand> Laden(IReadOnlyList<long> tourIds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tourIds is null || tourIds.Count is < 1 or > 10000 ||
            tourIds.Any(id => id <= 0) || tourIds.Distinct().Count() != tourIds.Count)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Tourauswahl ungültig.");

        using var connection = OeffneDatenbank.Ausfuehren(datenbankpfad);
        using var transaction = connection.BeginTransaction(deferred: true);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var all = TrainingseinheitenTabelle.Laden(connection)
                .Where(t => tourIds.Contains(t.Id))
                .ToArray();
            if (all.Length != tourIds.Count)
                throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Mindestens eine Tour wurde nicht gefunden.");

            var ordered = OrdneEtappen.Ausfuehren(all);
            var result = ordered.Select(t => new TourdetailQuelletappe(
                TourenMapper.ToAntwort(t),
                FindeAufzeichnung.Ausfuehren(t))).ToArray();

            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return Task.FromResult(new TourdetailQuellbestand(result));
        }
        catch
        {
            try { transaction.Rollback(); } catch (SqliteException) { }
            throw;
        }
    }
}
