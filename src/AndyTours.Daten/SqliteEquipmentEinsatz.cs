using AndyTours.Application.Auswertung;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Read-only SQLite-Zugriff auf die bestehende Equipment-Einsatzberechnung.</summary>
public sealed class SqliteEquipmentEinsatz : IEquipmentEinsatz
{
    private readonly string _databasePath;

    public SqliteEquipmentEinsatz(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        if (!Path.IsPathFullyQualified(databasePath)) throw new ArgumentException("Datenbankpfad muss absolut sein.", nameof(databasePath));
        _databasePath = Path.GetFullPath(databasePath);
    }

    public Task<EquipmentEinsatzAntwort> Laden(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_databasePath);
        using var tx = db.BeginTransaction(deferred: true);
        var einheiten = TrainingseinheitenTabelle.Laden(db);
        ct.ThrowIfCancellationRequested();
        var equipments = EquipmentsTabelle.Laden(db);
        ct.ThrowIfCancellationRequested();

        try
        {
            var rows = ErmittleEquipmenteinsatz.Ausfuehren(einheiten, equipments);
            ct.ThrowIfCancellationRequested();
            var result = new EquipmentEinsatzAntwort(Array.AsReadOnly(rows.Select(row =>
            {
                if ((row.VorherKm is double before && !double.IsFinite(before)) ||
                    (row.GesamtKm is double total && !double.IsFinite(total)))
                    throw Invalid();
                return new EquipmentEinsatzzeileAntwort(row.Name, row.VorherKm, row.GesamtKm, row.GesamtHm, row.IstSumme);
            }).ToArray()));
            ct.ThrowIfCancellationRequested();
            tx.Commit();
            return Task.FromResult(result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is OverflowException or ArgumentException or InvalidOperationException)
        {
            throw Invalid();
        }
    }

    private static UngueltigeTourException Invalid() => new("Equipment-Einsatz konnte nicht berechnet werden.");
}
