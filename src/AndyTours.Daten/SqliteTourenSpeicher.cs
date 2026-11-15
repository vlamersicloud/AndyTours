using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>SQLite-basierter Tourenspeicher mit festgelegtem serverseitigem Datenbankpfad.</summary>
public sealed class SqliteTourenSpeicher(string datenbankpfad) : ITourenSpeicher
{
    private SqliteConnection Oeffnen() => OeffneDatenbank.Ausfuehren(datenbankpfad);

    /// <inheritdoc />
    public Task<TourListeAntwort> Liste(int start, int anzahl, DateOnly? von, DateOnly? bis, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ValidiereTour.Liste(start, anzahl, von, bis);
        using var verbindung = Oeffnen();
        var alle = TrainingseinheitenTabelle.Laden(verbindung)
            .Where(t => (!von.HasValue || t.Datum >= von.Value) && (!bis.HasValue || t.Datum <= bis.Value))
            .ToList();
        var seite = alle.Skip(start).Take(anzahl).Select(TourenMapper.ToAntwort).ToArray();
        return Task.FromResult(new TourListeAntwort(seite, alle.Count, start, anzahl));
    }

    /// <inheritdoc />
    public Task<TourAntwort?> Lesen(long id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PruefeId(id);
        using var verbindung = Oeffnen();
        var tour = TrainingseinheitenTabelle.Laden(verbindung).SingleOrDefault(t => t.Id == id);
        return Task.FromResult(tour is null ? null : TourenMapper.ToAntwort(tour));
    }

    /// <inheritdoc />
    public Task<TourAntwort> Anlegen(TourWerte werte, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ValidiereTour.Ausfuehren(werte);
        using var verbindung = Oeffnen();
        var modell = TourenMapper.ToModell(werte);
        var eingefuegt = TrainingseinheitenTabelle.Einfuegen(verbindung, modell);
        modell.Id = eingefuegt.Id;
        modell.FahrerId = eingefuegt.FahrerId;
        var gespeichert = TrainingseinheitenTabelle.Laden(verbindung).Single(t => t.Id == modell.Id);
        return Task.FromResult(TourenMapper.ToAntwort(gespeichert));
    }

    /// <inheritdoc />
    public Task<TourAntwort> Aendern(long id, TourAendernAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PruefeId(id);
        if (anfrage is null) throw new UngueltigeTourException("Änderungsanforderung fehlt.");
        PruefeVersion(anfrage.Version);
        ValidiereTour.Ausfuehren(anfrage.Werte);
        if (anfrage.Werte.FahrerId <= 0) throw new UngueltigeTourException("Fahrerkennung fehlt.");

        using var verbindung = Oeffnen();
        using var transaktion = verbindung.BeginTransaction(deferred: false);
        var aktuell = TrainingseinheitenTabelle.Laden(verbindung).SingleOrDefault(t => t.Id == id);
        if (aktuell is null) throw new TourNichtGefundenException(id);
        if (TourenMapper.ToAntwort(aktuell).Version != anfrage.Version) throw new TourVersionskonfliktException();

        var modell = TourenMapper.ToModell(anfrage.Werte, id);
        TrainingseinheitenTabelle.AendereInTransaktion(verbindung, transaktion, modell);
        var gespeichert = TrainingseinheitenTabelle.Laden(verbindung).Single(t => t.Id == id);
        transaktion.Commit();
        return Task.FromResult(TourenMapper.ToAntwort(gespeichert));
    }

    /// <inheritdoc />
    public Task Loeschen(long id, string version, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PruefeId(id);
        PruefeVersion(version);
        using var verbindung = Oeffnen();
        using var transaktion = verbindung.BeginTransaction(deferred: false);
        var aktuell = TrainingseinheitenTabelle.Laden(verbindung).SingleOrDefault(t => t.Id == id);
        if (aktuell is null) throw new TourNichtGefundenException(id);
        if (TourenMapper.ToAntwort(aktuell).Version != version) throw new TourVersionskonfliktException();

        using var befehl = verbindung.CreateCommand();
        befehl.Transaction = transaktion;
        befehl.CommandText = "DELETE FROM Trainingseinheit WHERE Id=@id";
        befehl.Parameters.AddWithValue("@id", id);
        befehl.ExecuteNonQuery();
        transaktion.Commit();
        return Task.CompletedTask;
    }

    private static void PruefeId(long id)
    {
        if (id <= 0) throw new UngueltigeTourException("Tour-ID muss positiv sein.");
    }

    private static void PruefeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) throw new UngueltigeTourException("Versionskennung fehlt.");
    }
}
