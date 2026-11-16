using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Reads the configured Garmin key and deduplication mapping for server-only import work.</summary>
public sealed class SqliteGarminImportServerdaten(string datenbankpfad) : IGarminImportServerdaten
{
    public Task<GarminImportServerkontext> Laden(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        var key = EinstellungTabelle.Lesen(db, Einstellungsschluessel.GarminPersonalApiKey);
        if (string.IsNullOrWhiteSpace(key))
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Garmin-Schlüssel ist nicht konfiguriert.");
        var existingTourIds = TrainingseinheitenTabelle.Laden(db).Select(tour => tour.Id).ToHashSet();
        IReadOnlySet<long> imported = GarminImportZuordnung.Laden(db, existingTourIds);
        ct.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(new GarminImportServerkontext(key, imported));
    }
}
