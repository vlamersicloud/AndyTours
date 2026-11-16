using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Implementiert das versionierte Löschen als Operation des Application-Ports.</summary>
public sealed partial class SqliteTourenMehrfachverwaltung : ITourenMehrfachverwaltung
{
    /// <summary>Löscht alle referenzierten Touren atomar; Anhangdateien werden nicht verändert.</summary>
    public Task<TourenMehrfachAntwort> Loeschen(TourenLoeschenAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = TourenMehrfachRegeln.Normalisieren(anfrage);
        var hash = Hash("loeschen", normalized);
        var response = Run(normalized.AnfrageId, hash, normalized.Touren, (connection, transaction, selected) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ids = selected.Select(x => x.Id).ToHashSet();
            GarminImportZuordnung.EntferneFuerEinheiten(connection, transaction, ids);

            foreach (var id in normalized.Touren.Select(x => x.Id))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM Trainingseinheit WHERE Id = $id;";
                command.Parameters.AddWithValue("$id", id);
                if (command.ExecuteNonQuery() != 1)
                    throw new TourNichtGefundenException(id);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new TourenMehrfachAntwort(normalized.AnfrageId, Array.Empty<TourAntwort>(),
                Array.AsReadOnly(normalized.Touren.Select(x => x.Id).Order().ToArray()));
        }, cancellationToken);
        return Task.FromResult(response);
    }
}
