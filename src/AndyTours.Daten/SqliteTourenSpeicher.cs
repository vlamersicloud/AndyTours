using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>SQLite-basierter Tourenspeicher mit festgelegtem serverseitigem Datenbankpfad.</summary>
public sealed class SqliteTourenSpeicher(string datenbankpfad) : ITourenSpeicher
{
    private SqliteConnection Oeffnen() => OeffneDatenbank.Ausfuehren(datenbankpfad);

    /// <inheritdoc />
    public Task<TourListeAntwort> Liste(int start, int anzahl, DateOnly? von, DateOnly? bis, CancellationToken ct = default) =>
        ListeGefiltert(start, anzahl, new TourenListenfilter(von, bis, null, null), ct);

    /// <inheritdoc />
    public Task<TourListeAntwort> ListeGefiltert(int start, int anzahl, TourenListenfilter filter, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (filter is null) throw new UngueltigeTourException("Tourenfilter fehlt.");
        ValidiereTour.Liste(start, anzahl, filter.Von, filter.Bis);
        if (filter.AktivitaetId is <= 0 || filter.KategorieId is <= 0)
            throw new UngueltigeTourException("Aktivitäts- und Kategorienkennungen müssen positiv sein.");
        using var verbindung = Oeffnen();
        var alle = TrainingseinheitenTabelle.Laden(verbindung)
            .Where(t => (!filter.Von.HasValue || t.Datum >= filter.Von.Value) &&
                        (!filter.Bis.HasValue || t.Datum <= filter.Bis.Value) &&
                        (!filter.AktivitaetId.HasValue || t.AktivitaetId == filter.AktivitaetId.Value) &&
                        (!filter.KategorieId.HasValue || t.KategorieIds.Contains(filter.KategorieId.Value)))
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
    public Task<TourAntwort> Anlegen(TourWerte werte, CancellationToken ct = default) =>
        AnlegenIdempotent(werte, Guid.NewGuid(), ct);

    /// <inheritdoc />
    public Task<TourAntwort> AnlegenIdempotent(TourWerte werte, Guid anfrageId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (anfrageId == Guid.Empty) throw new UngueltigeTourException("Idempotenzkennung ist ungültig.");
        ValidiereTour.Ausfuehren(werte);
        var hash = RequestHash(werte);

        try
        {
            var vorhandenerReceipt = LeseReceipt(anfrageId);
            if (vorhandenerReceipt is not null)
                return Task.FromResult(PruefeReceipt(vorhandenerReceipt.Value, hash));

            using var verbindung = Oeffnen();
            TourAntwort? antwort = null;
            var modell = TourenMapper.ToModell(werte);
            var eingefuegt = TrainingseinheitenTabelle.Einfuegen(verbindung, modell, (c, transaktion, id) =>
            {
                var gespeichert = TrainingseinheitenTabelle.Laden(c).Single(t => t.Id == id);
                antwort = TourenMapper.ToAntwort(gespeichert);
                using var befehl = c.CreateCommand();
                befehl.Transaction = transaktion;
                befehl.CommandText = """
                    INSERT INTO ApiTourAnlage (AnfrageId, AnfrageHash, AntwortJson)
                    VALUES (@AnfrageId, @AnfrageHash, @AntwortJson);
                    """;
                befehl.Parameters.AddWithValue("@AnfrageId", anfrageId.ToString("D"));
                befehl.Parameters.AddWithValue("@AnfrageHash", hash);
                befehl.Parameters.AddWithValue("@AntwortJson", JsonSerializer.Serialize(antwort));
                befehl.ExecuteNonQuery();
            });

            modell.Id = eingefuegt.Id;
            modell.FahrerId = eingefuegt.FahrerId;
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(antwort ?? throw new InvalidOperationException("Tourantwort fehlt."));
        }
        catch (SqliteException fehler) when (fehler.SqliteErrorCode == 19)
        {
            // UNIQUE-Rennen: nur ein bereits committeter Receipt mit derselben Anfrage darf replayen.
            var receipt = LeseReceipt(anfrageId);
            if (receipt is not null) return Task.FromResult(PruefeReceipt(receipt.Value, hash));
            throw;
        }
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

        ct.ThrowIfCancellationRequested();
        GarminImportZuordnung.EntferneFuerEinheiten(verbindung, transaktion, new HashSet<long> { id });
        using var befehl = verbindung.CreateCommand();
        befehl.Transaction = transaktion;
        befehl.CommandText = "DELETE FROM Trainingseinheit WHERE Id=@id";
        befehl.Parameters.AddWithValue("@id", id);
        befehl.ExecuteNonQuery();
        ct.ThrowIfCancellationRequested();
        transaktion.Commit();
        return Task.CompletedTask;
    }

    private (string Hash, string AntwortJson)? LeseReceipt(Guid anfrageId)
    {
        using var verbindung = Oeffnen();
        using var befehl = verbindung.CreateCommand();
        befehl.CommandText = "SELECT AnfrageHash, AntwortJson FROM ApiTourAnlage WHERE AnfrageId = @AnfrageId;";
        befehl.Parameters.AddWithValue("@AnfrageId", anfrageId.ToString("D"));
        using var leser = befehl.ExecuteReader();
        return leser.Read() ? (leser.GetString(0), leser.GetString(1)) : null;
    }

    private static TourAntwort PruefeReceipt((string Hash, string AntwortJson) receipt, string hash)
    {
        if (!string.Equals(receipt.Hash, hash, StringComparison.Ordinal))
            throw new UngueltigeTourException("Idempotenzkennung wurde bereits mit anderen Tourdaten verwendet.");
        return JsonSerializer.Deserialize<TourAntwort>(receipt.AntwortJson)
            ?? throw new InvalidOperationException("Gespeicherte Tourantwort ist ungültig.");
    }

    private static string RequestHash(TourWerte werte)
    {
        var kanonisch = werte with { KategorieIds = werte.KategorieIds.Order().ToArray() };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(kanonisch)));
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
