using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Führt versionierte Kategorienänderungen atomar in SQLite aus.</summary>
public sealed class SqliteKategorienVerwaltung(string datenbankpfad) : IKategorienVerwaltung
{
    private SqliteConnection Oeffnen() => OeffneDatenbank.Ausfuehren(datenbankpfad);

    /// <inheritdoc />
    public Task<KategorienAntwort> Laden(CancellationToken ct = default)
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
    public Task<KategorienAntwort> Anlegen(KategorieSpeichernAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PruefeAnfrage(anfrage);
        PruefeName(anfrage.Name);
        using var verbindung = Oeffnen();
        using var transaktion = verbindung.BeginTransaction(deferred: false);
        PruefeVersion(verbindung, anfrage.Version);
        var kategorien = KategorienTabelle.Laden(verbindung);
        PruefeEindeutigenNamen(kategorien, anfrage.Name);
        KategorienTabelle.Einfuegen(verbindung, transaktion,
            new Kategorie { Name = anfrage.Name.Trim(), IstStandard = false });
        var antwort = Antwort(verbindung);
        ct.ThrowIfCancellationRequested();
        transaktion.Commit();
        return Task.FromResult(antwort);
    }

    /// <inheritdoc />
    public Task<KategorienAntwort> Umbenennen(long id, KategorieSpeichernAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PruefeId(id);
        PruefeAnfrage(anfrage);
        PruefeName(anfrage.Name);
        using var verbindung = Oeffnen();
        using var transaktion = verbindung.BeginTransaction(deferred: false);
        PruefeVersion(verbindung, anfrage.Version);
        var kategorien = KategorienTabelle.Laden(verbindung);
        var kategorie = kategorien.SingleOrDefault(k => k.Id == id)
            ?? throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Kategorie wurde nicht gefunden.");
        PruefeEindeutigenNamen(kategorien.Where(k => k.Id != id), anfrage.Name);
        var alterName = kategorie.Name;
        kategorie.Name = anfrage.Name.Trim();
        KategorienTabelle.Aendern(verbindung, transaktion, kategorie);
        EquipmentsTabelle.ErsetzeStandardKategorie(verbindung, transaktion, alterName, kategorie.Name);
        var antwort = Antwort(verbindung);
        ct.ThrowIfCancellationRequested();
        transaktion.Commit();
        return Task.FromResult(antwort);
    }

    /// <inheritdoc />
    public Task<KategorienAntwort> Loeschen(long id, string version, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        PruefeId(id);
        PruefeVersionEingabe(version);
        using var verbindung = Oeffnen();
        using var transaktion = verbindung.BeginTransaction(deferred: false);
        PruefeVersion(verbindung, version);
        var kategorien = KategorienTabelle.Laden(verbindung);
        if (!kategorien.Any(k => k.Id == id))
            throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Kategorie wurde nicht gefunden.");
        var sperre = ErmittleLoeschsperre.Kategorie(DatenbestandTabellen.Laden(verbindung), id);
        if (sperre is not null)
            throw new StammdatenException(StammdatenFehlerart.Konflikt, sperre);
        KategorienTabelle.Loeschen(verbindung, transaktion, id);
        var antwort = Antwort(verbindung);
        ct.ThrowIfCancellationRequested();
        transaktion.Commit();
        return Task.FromResult(antwort);
    }

    /// <inheritdoc />
    public Task<KategorienAntwort> SetzeStandard(KategorieStandardAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (anfrage is null) throw UngueltigeAnfrage();
        PruefeVersionEingabe(anfrage.Version);
        PruefeId(anfrage.KategorieId);
        using var verbindung = Oeffnen();
        using var transaktion = verbindung.BeginTransaction(deferred: false);
        PruefeVersion(verbindung, anfrage.Version);
        var kategorien = KategorienTabelle.Laden(verbindung);
        var kategorie = kategorien.SingleOrDefault(k => k.Id == anfrage.KategorieId)
            ?? throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Kategorie wurde nicht gefunden.");
        if (kategorie.IstStandard != anfrage.IstStandard)
        {
            kategorie.IstStandard = anfrage.IstStandard;
            KategorienTabelle.Aendern(verbindung, transaktion, kategorie);
        }
        var antwort = Antwort(verbindung);
        ct.ThrowIfCancellationRequested();
        transaktion.Commit();
        return Task.FromResult(antwort);
    }

    /// <inheritdoc />
    public Task<KategorienAntwort> Zusammenfuehren(KategorienZusammenfuehrenAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (anfrage is null) throw UngueltigeAnfrage();
        PruefeVersionEingabe(anfrage.Version);
        PruefeId(anfrage.QuelleId);
        PruefeId(anfrage.ZielId);
        if (anfrage.QuelleId == anfrage.ZielId)
            throw UngueltigeAnfrage("Quell- und Zielkategorie müssen verschieden sein.");
        using var verbindung = Oeffnen();
        using var transaktion = verbindung.BeginTransaction(deferred: false);
        PruefeVersion(verbindung, anfrage.Version);
        var kategorien = KategorienTabelle.Laden(verbindung);
        var quelle = kategorien.SingleOrDefault(k => k.Id == anfrage.QuelleId)
            ?? throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Quellkategorie wurde nicht gefunden.");
        var ziel = kategorien.SingleOrDefault(k => k.Id == anfrage.ZielId)
            ?? throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Zielkategorie wurde nicht gefunden.");

        KategorienTabelle.FuehreZusammen(verbindung, transaktion, quelle.Id, ziel.Id);
        EquipmentsTabelle.ErsetzeStandardKategorie(verbindung, transaktion, quelle.Name, ziel.Name);
        var antwort = Antwort(verbindung);
        ct.ThrowIfCancellationRequested();
        transaktion.Commit();
        return Task.FromResult(antwort);
    }

    private static KategorienAntwort Antwort(SqliteConnection verbindung)
    {
        var eintraege = KategorienTabelle.Laden(verbindung)
            .Select(k => new KategorieAntwort(k.Id, k.Name, k.IstStandard))
            .ToArray();
        var version = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(eintraege)));
        return new KategorienAntwort(version, eintraege);
    }

    private static void PruefeVersion(SqliteConnection verbindung, string version)
    {
        var aktuell = Antwort(verbindung).Version;
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(aktuell), Convert.FromHexString(version)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt,
                "Kategorien wurden zwischenzeitlich geändert. Bitte neu laden.");
    }

    private static void PruefeVersionEingabe(string? version)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Length != 64 || !version.All(Uri.IsHexDigit))
            throw UngueltigeAnfrage("Versionskennung fehlt oder ist ungültig.");
    }

    private static void PruefeAnfrage(KategorieSpeichernAnfrage? anfrage)
    {
        if (anfrage is null) throw UngueltigeAnfrage();
        PruefeVersionEingabe(anfrage.Version);
    }

    private static void PruefeName(string? name)
    {
        if (name is null) throw UngueltigeAnfrage("Kategoriename fehlt.");
        var trimmed = name.Trim();
        if (trimmed.Length == 0 || trimmed.Length > 500 || trimmed.Any(char.IsControl))
            throw UngueltigeAnfrage("Kategoriename muss 1 bis 500 Zeichen enthalten und darf keine Steuerzeichen enthalten.");
    }

    private static void PruefeEindeutigenNamen(IEnumerable<Kategorie> kategorien, string name)
    {
        var normalized = name.Trim();
        if (kategorien.Any(k => string.Equals(k.Name.Trim(), normalized, StringComparison.OrdinalIgnoreCase)))
            throw UngueltigeAnfrage("Eine Kategorie mit diesem Namen existiert bereits.");
    }

    private static void PruefeId(long id)
    {
        if (id <= 0) throw UngueltigeAnfrage("Kategoriekennung muss positiv sein.");
    }

    private static StammdatenException UngueltigeAnfrage(string text = "Kategorienanforderung ist ungültig.") =>
        new(StammdatenFehlerart.Ungueltig, text);
}
