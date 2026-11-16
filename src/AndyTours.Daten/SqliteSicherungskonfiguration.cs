namespace AndyTours.Daten;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten.Datenordner;
using Microsoft.Data.Sqlite;

public sealed class SqliteSicherungskonfiguration : ISicherungskonfiguration
{
    private readonly string _databasePath;

    public SqliteSicherungskonfiguration(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Datenbankpfad erforderlich.", nameof(databasePath));
        _databasePath = Path.GetFullPath(databasePath);
    }

    public Task<SicherungskonfigurationAntwort> Laden(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_databasePath);
        using var tx = db.BeginTransaction(deferred: true);
        cancellationToken.ThrowIfCancellationRequested();
        var response = Read(db);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(response);
    }

    public Task<SicherungskonfigurationAntwort> Speichern(SicherungskonfigurationAnfrage anfrage, CancellationToken cancellationToken)
    {
        SicherungskonfigurationRegeln.Pruefe(anfrage);
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_databasePath);
        using var tx = db.BeginTransaction(deferred: false);
        var current = Read(db);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(anfrage.Version), Convert.FromHexString(current.Version)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt,
                "Sicherungskonfiguration wurde zwischenzeitlich geändert.");

        cancellationToken.ThrowIfCancellationRequested();
        EinstellungTabelle.Schreiben(db, tx, Einstellungsschluessel.Sicherungsintervall,
            anfrage.Intervall.ToString(CultureInfo.InvariantCulture));
        EinstellungTabelle.Schreiben(db, tx, Einstellungsschluessel.Sicherungshoechstzahl,
            anfrage.Hoechstzahl.ToString(CultureInfo.InvariantCulture));
        cancellationToken.ThrowIfCancellationRequested();
        var updated = Read(db);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(updated);
    }

    private static SicherungskonfigurationAntwort Read(SqliteConnection db)
    {
        var settings = SicherungEinstellung.Laden(db);
        var rawInterval = EinstellungTabelle.Lesen(db, Einstellungsschluessel.Sicherungsintervall) ?? string.Empty;
        var rawMaximum = EinstellungTabelle.Lesen(db, Einstellungsschluessel.Sicherungshoechstzahl) ?? string.Empty;
        var versionJson = JsonSerializer.Serialize(new { Intervall = rawInterval, Hoechstzahl = rawMaximum });
        var version = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(versionJson)));
        return new SicherungskonfigurationAntwort(version, settings.Intervall, settings.Hoechstzahl,
            settings.StartsSeitSicherung);
    }
}
