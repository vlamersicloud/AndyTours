using System.Security.Cryptography;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteTourenMehrfachverwaltung
{
    private TourenMehrfachAntwort Run(
        Guid anfrageId,
        string anfrageHash,
        IReadOnlyList<TourVersionsreferenz> referenzen,
        Func<SqliteConnection, SqliteTransaction, IReadOnlyList<Trainingseinheit>, TourenMehrfachAntwort> mutieren,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = OeffneDatenbank.Ausfuehren(_databasePath);
        using var transaction = connection.BeginTransaction(deferred: false);
        TourenMehrfachSchema.Ensure(connection, transaction);

        var receipt = LeseQuittung(connection, transaction, anfrageId, anfrageHash);
        if (receipt is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return receipt;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var alle = TrainingseinheitenTabelle.Laden(connection);
        var ausgewaehlt = new List<Trainingseinheit>(referenzen.Count);
        foreach (var referenz in referenzen)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var aktuell = alle.SingleOrDefault(t => t.Id == referenz.Id)
                ?? throw new TourNichtGefundenException(referenz.Id);
            var version = TourenMapper.ToAntwort(aktuell).Version;
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(version), Convert.FromHexString(referenz.Version)))
                throw new TourVersionskonfliktException();
            ausgewaehlt.Add(aktuell);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var antwort = mutieren(connection, transaction, ausgewaehlt);
        ValidiereAntwort(anfrageId, referenzen, antwort);
        cancellationToken.ThrowIfCancellationRequested();
        SchreibeQuittung(connection, transaction, anfrageId, anfrageHash, antwort);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return antwort;
    }

    private static void ValidiereAntwort(
        Guid anfrageId, IReadOnlyList<TourVersionsreferenz> referenzen, TourenMehrfachAntwort? antwort)
    {
        if (antwort is null || antwort.AnfrageId != anfrageId ||
            antwort.Eintraege is null || antwort.GeloeschteIds is null)
            throw new InvalidOperationException("Mehrfachaktion lieferte eine ungültige Antwort.");

        var erwarteteIds = referenzen.Select(r => r.Id).Order().ToArray();
        var geaenderteIds = antwort.Eintraege.Select(e => e?.Id ?? 0).Order().ToArray();
        var geloeschteIds = antwort.GeloeschteIds.Order().ToArray();
        var nurGeaendert = geloeschteIds.Length == 0 &&
            geaenderteIds.Length == erwarteteIds.Length &&
            geaenderteIds.All(id => id > 0) &&
            geaenderteIds.Distinct().Count() == geaenderteIds.Length &&
            geaenderteIds.SequenceEqual(erwarteteIds) &&
            antwort.Eintraege.All(e => e is not null && e.Werte is not null);
        var nurGeloescht = antwort.Eintraege.Count == 0 &&
            geloeschteIds.Length == erwarteteIds.Length &&
            geloeschteIds.All(id => id > 0) &&
            geloeschteIds.Distinct().Count() == geloeschteIds.Length &&
            geloeschteIds.SequenceEqual(erwarteteIds);
        if (!nurGeaendert && !nurGeloescht)
            throw new InvalidOperationException("Mehrfachaktion lieferte eine ungültige Antwort.");
    }
}
