using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Führt versionierte Aktivitätsänderungen in SQLite-Transaktionen aus.</summary>
public sealed class SqliteAktivitaetenVerwaltung(string datenbankpfad) : IAktivitaetenVerwaltung
{
    private SqliteConnection Oeffnen() => OeffneDatenbank.Ausfuehren(datenbankpfad);

    /// <inheritdoc />
    public Task<AktivitaetenAntwort> Laden(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var verbindung = Oeffnen();
        using var transaktion = verbindung.BeginTransaction(deferred: true);
        var antwort = Antwort(verbindung);
        ct.ThrowIfCancellationRequested();
        transaktion.Commit();
        return Task.FromResult(antwort);
    }

    /// <inheritdoc />
    public Task<AktivitaetenAntwort> Anlegen(AktivitaetSpeichernAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PruefeAnfrage(anfrage);
        PruefeName(anfrage.Name);
        using var verbindung = Oeffnen();
        using var transaktion = verbindung.BeginTransaction(deferred: false);
        PruefeVersion(verbindung, anfrage.Version);
        var aktivitaeten = AktivitaetenTabelle.Laden(verbindung);
        PruefeEindeutigenNamen(aktivitaeten, anfrage.Name);
        var haupt = !aktivitaeten.Any(a => a.IstHaupt);
        var neueAktivitaet = new Aktivitaet { Name = anfrage.Name.Trim(), IstHaupt = haupt };
        var id = AktivitaetenTabelle.Einfuegen(verbindung, transaktion, neueAktivitaet);
        var ids = OrdneAktivitaeten.Ausfuehren(aktivitaeten, AktivitaetenReihenfolgeEinstellung.Laden(verbindung))
            .Select(a => a.Id).Append(id).ToArray();
        AktivitaetenReihenfolgeEinstellung.Speichern(verbindung, transaktion, ids);
        var antwort = Antwort(verbindung);
        ct.ThrowIfCancellationRequested();
        transaktion.Commit();
        return Task.FromResult(antwort);
    }

    /// <inheritdoc />
    public Task<AktivitaetenAntwort> Umbenennen(long id, AktivitaetSpeichernAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PruefeId(id);
        PruefeAnfrage(anfrage);
        PruefeName(anfrage.Name);
        using var verbindung = Oeffnen();
        using var transaktion = verbindung.BeginTransaction(deferred: false);
        PruefeVersion(verbindung, anfrage.Version);
        var aktivitaeten = AktivitaetenTabelle.Laden(verbindung);
        var aktivitaet = aktivitaeten.SingleOrDefault(a => a.Id == id)
            ?? throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Aktivität wurde nicht gefunden.");
        PruefeEindeutigenNamen(aktivitaeten.Where(a => a.Id != id), anfrage.Name);
        aktivitaet.Name = anfrage.Name.Trim();
        AktivitaetenTabelle.Aendern(verbindung, transaktion, aktivitaet);
        var antwort = Antwort(verbindung);
        ct.ThrowIfCancellationRequested();
        transaktion.Commit();
        return Task.FromResult(antwort);
    }

    /// <inheritdoc />
    public Task<AktivitaetenAntwort> Loeschen(long id, string version, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PruefeId(id);
        PruefeVersionEingabe(version);
        using var verbindung = Oeffnen();
        using var transaktion = verbindung.BeginTransaction(deferred: false);
        PruefeVersion(verbindung, version);
        var aktivitaeten = AktivitaetenTabelle.Laden(verbindung);
        if (!aktivitaeten.Any(a => a.Id == id))
            throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Aktivität wurde nicht gefunden.");
        var sperre = ErmittleLoeschsperre.Aktivitaet(DatenbestandTabellen.Laden(verbindung), id);
        if (sperre is not null)
            throw new StammdatenException(StammdatenFehlerart.Konflikt, sperre);
        AktivitaetenTabelle.Loeschen(verbindung, transaktion, id);
        var ids = OrdneAktivitaeten.Ausfuehren(aktivitaeten, AktivitaetenReihenfolgeEinstellung.Laden(verbindung))
            .Where(a => a.Id != id).Select(a => a.Id).ToArray();
        AktivitaetenReihenfolgeEinstellung.Speichern(verbindung, transaktion, ids);
        var antwort = Antwort(verbindung);
        ct.ThrowIfCancellationRequested();
        transaktion.Commit();
        return Task.FromResult(antwort);
    }

    /// <inheritdoc />
    public Task<AktivitaetenAntwort> SetzeHauptaktivitaet(AktivitaetAuswahlAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (anfrage is null) throw UngueltigeAnfrage();
        PruefeVersionEingabe(anfrage.Version);
        PruefeId(anfrage.AktivitaetId);
        using var verbindung = Oeffnen();
        using var transaktion = verbindung.BeginTransaction(deferred: false);
        PruefeVersion(verbindung, anfrage.Version);
        var aktivitaeten = AktivitaetenTabelle.Laden(verbindung);
        if (!aktivitaeten.Any(a => a.Id == anfrage.AktivitaetId))
            throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Aktivität wurde nicht gefunden.");
        foreach (var aktivitaet in aktivitaeten)
        {
            if (aktivitaet.IstHaupt != (aktivitaet.Id == anfrage.AktivitaetId))
            {
                aktivitaet.IstHaupt = aktivitaet.Id == anfrage.AktivitaetId;
                AktivitaetenTabelle.Aendern(verbindung, transaktion, aktivitaet);
            }
        }
        var antwort = Antwort(verbindung);
        ct.ThrowIfCancellationRequested();
        transaktion.Commit();
        return Task.FromResult(antwort);
    }

    /// <inheritdoc />
    public Task<AktivitaetenAntwort> SetzeReihenfolge(AktivitaetenReihenfolgeAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (anfrage is null) throw UngueltigeAnfrage();
        PruefeVersionEingabe(anfrage.Version);
        if (anfrage.Ids is null) throw UngueltigeAnfrage();
        using var verbindung = Oeffnen();
        using var transaktion = verbindung.BeginTransaction(deferred: false);
        PruefeVersion(verbindung, anfrage.Version);
        var aktuell = Antwort(verbindung);
        if (anfrage.Ids.Count != aktuell.Eintraege.Count ||
            anfrage.Ids.Any(id => id <= 0) ||
            anfrage.Ids.Distinct().Count() != anfrage.Ids.Count ||
            !anfrage.Ids.ToHashSet().SetEquals(aktuell.Eintraege.Select(a => a.Id)))
            throw UngueltigeAnfrage("Die Reihenfolge muss jede vorhandene Aktivität genau einmal enthalten.");
        AktivitaetenReihenfolgeEinstellung.Speichern(verbindung, transaktion, anfrage.Ids);
        var antwort = Antwort(verbindung);
        ct.ThrowIfCancellationRequested();
        transaktion.Commit();
        return Task.FromResult(antwort);
    }

    /// <inheritdoc />
    public Task<AktivitaetenAntwort> Zusammenfuehren(AktivitaetenZusammenfuehrenAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (anfrage is null) throw UngueltigeAnfrage();
        PruefeVersionEingabe(anfrage.Version);
        PruefeId(anfrage.QuelleId);
        PruefeId(anfrage.ZielId);
        if (anfrage.QuelleId == anfrage.ZielId)
            throw UngueltigeAnfrage("Quell- und Zielaktivität müssen verschieden sein.");
        using var verbindung = Oeffnen();
        using var transaktion = verbindung.BeginTransaction(deferred: false);
        PruefeVersion(verbindung, anfrage.Version);
        var aktivitaeten = AktivitaetenTabelle.Laden(verbindung);
        if (!aktivitaeten.Any(a => a.Id == anfrage.QuelleId) || !aktivitaeten.Any(a => a.Id == anfrage.ZielId))
            throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Quell- oder Zielaktivität wurde nicht gefunden.");
        AktivitaetenTabelle.FuehreZusammen(verbindung, transaktion, anfrage.QuelleId, anfrage.ZielId);
        var ids = OrdneAktivitaeten.Ausfuehren(aktivitaeten, AktivitaetenReihenfolgeEinstellung.Laden(verbindung))
            .Where(a => a.Id != anfrage.QuelleId).Select(a => a.Id).ToArray();
        AktivitaetenReihenfolgeEinstellung.Speichern(verbindung, transaktion, ids);
        var antwort = Antwort(verbindung);
        ct.ThrowIfCancellationRequested();
        transaktion.Commit();
        return Task.FromResult(antwort);
    }

    private static AktivitaetenAntwort Antwort(SqliteConnection verbindung)
    {
        var eintraege = OrdneAktivitaeten.Ausfuehren(
                AktivitaetenTabelle.Laden(verbindung),
                AktivitaetenReihenfolgeEinstellung.Laden(verbindung))
            .Select(a => new AktivitaetAntwort(a.Id, a.Name, a.IstHaupt, a.Geschwindigkeitseinheit,
                a.ZielKm, a.ZielHm, a.ZielAnzahl))
            .ToArray();
        var version = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(eintraege)));
        return new AktivitaetenAntwort(version, eintraege);
    }

    private static void PruefeVersion(SqliteConnection verbindung, string version)
    {
        var aktuell = Antwort(verbindung).Version;
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(aktuell), Convert.FromHexString(version)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt,
                "Aktivitäten wurden zwischenzeitlich geändert. Bitte neu laden.");
    }

    private static void PruefeVersionEingabe(string? version)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Length != 64 ||
            !version.All(Uri.IsHexDigit))
            throw UngueltigeAnfrage("Versionskennung fehlt oder ist ungültig.");
    }

    private static void PruefeAnfrage(AktivitaetSpeichernAnfrage? anfrage)
    {
        if (anfrage is null) throw UngueltigeAnfrage();
        PruefeVersionEingabe(anfrage.Version);
    }

    private static void PruefeName(string? name)
    {
        if (name is null) throw UngueltigeAnfrage("Aktivitätsname fehlt.");
        var trimmed = name.Trim();
        if (trimmed.Length == 0 || trimmed.Length > 500 || trimmed.Any(char.IsControl))
            throw UngueltigeAnfrage("Aktivitätsname muss 1 bis 500 Zeichen enthalten und darf keine Steuerzeichen enthalten.");
    }

    private static void PruefeEindeutigenNamen(IEnumerable<Aktivitaet> aktivitaeten, string name)
    {
        var normalized = name.Trim();
        if (aktivitaeten.Any(a => string.Equals(a.Name.Trim(), normalized, StringComparison.OrdinalIgnoreCase)))
            throw UngueltigeAnfrage("Eine Aktivität mit diesem Namen existiert bereits.");
    }

    private static void PruefeId(long id)
    {
        if (id <= 0) throw UngueltigeAnfrage("Aktivitätskennung muss positiv sein.");
    }

    private static StammdatenException UngueltigeAnfrage(string text = "Aktivitätsanforderung ist ungültig.") =>
        new(StammdatenFehlerart.Ungueltig, text);
}
