using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Verwaltet Equipments versioniert und atomar in SQLite.</summary>
public sealed class SqliteEquipmentsVerwaltung(string datenbankpfad) : IEquipmentsVerwaltung
{
    private SqliteConnection Oeffnen() => OeffneDatenbank.Ausfuehren(datenbankpfad);

    public Task<EquipmentsAntwort> Laden(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var c = Oeffnen(); using var tx = c.BeginTransaction(deferred: true);
        var result = Antwort(c); ct.ThrowIfCancellationRequested(); tx.Commit(); return Task.FromResult(result);
    }

    public Task<EquipmentsAntwort> Anlegen(EquipmentSpeichernAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); PruefeAnfrage(anfrage);
        using var c = Oeffnen(); using var tx = c.BeginTransaction(deferred: false); PruefeVersion(c, anfrage.Version);
        var bestand = DatenbestandTabellen.Laden(c); var werte = PruefeWerte(c, anfrage.Werte, null, null);
        PruefeName(bestand.Equipments, werte.Name);
        var neu = AusModell(werte, 0); neu.IstStandard = neu.IstStandard && neu.AktivitaetId.HasValue && !neu.Ausgemustert;
        var aenderungen = UebernimmEquipment.ErmittleAenderungen(bestand, neu);
        var id = EquipmentsTabelle.Einfuegen(c, tx, aenderungen[0]); aenderungen[0].Id = id;
        foreach (var anderes in aenderungen.Skip(1)) EquipmentsTabelle.Aendern(c, tx, anderes);
        var result = Antwort(c); ct.ThrowIfCancellationRequested(); tx.Commit(); return Task.FromResult(result);
    }

    public Task<EquipmentsAntwort> Aendern(long id, EquipmentSpeichernAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); PruefeId(id); PruefeAnfrage(anfrage);
        using var c = Oeffnen(); using var tx = c.BeginTransaction(deferred: false); PruefeVersion(c, anfrage.Version);
        var bestand = DatenbestandTabellen.Laden(c); var alt = bestand.Equipments.SingleOrDefault(e => e.Id == id)
            ?? throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Equipment wurde nicht gefunden.");
        var werte = PruefeWerte(c, anfrage.Werte, alt, bestand.Kategorien);
        PruefeName(bestand.Equipments.Where(e => e.Id != id), werte.Name);
        var neu = AusModell(werte, id); neu.IstStandard = neu.IstStandard && neu.AktivitaetId.HasValue && !neu.Ausgemustert;
        var aenderungen = UebernimmEquipment.ErmittleAenderungen(bestand, neu);
        foreach (var e in aenderungen) EquipmentsTabelle.Aendern(c, tx, e);
        var result = Antwort(c); ct.ThrowIfCancellationRequested(); tx.Commit(); return Task.FromResult(result);
    }

    public Task<EquipmentsAntwort> Loeschen(long id, string version, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); PruefeId(id); PruefeVersionEingabe(version);
        using var c = Oeffnen(); using var tx = c.BeginTransaction(deferred: false); PruefeVersion(c, version);
        var bestand = DatenbestandTabellen.Laden(c);
        if (!bestand.Equipments.Any(e => e.Id == id)) throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Equipment wurde nicht gefunden.");
        var sperre = ErmittleLoeschsperre.Equipment(bestand, id);
        if (sperre is not null) throw new StammdatenException(StammdatenFehlerart.Konflikt, sperre);
        EquipmentsTabelle.Loeschen(c, tx, id);
        var result = Antwort(c); ct.ThrowIfCancellationRequested(); tx.Commit(); return Task.FromResult(result);
    }

    private static EquipmentsAntwort Antwort(SqliteConnection c)
    {
        var entries = EquipmentsTabelle.Laden(c).Select(e => new EquipmentAntwort(e.Id, e.Name, e.AktivitaetId,
            e.Geraetezuordnung, e.StandardKategorie, e.IstStandard, e.Ausgemustert, e.Anschaffungsdatum, e.AnfangsdistanzKm)).ToArray();
        return new EquipmentsAntwort(Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(entries))), entries);
    }
    private static void PruefeVersion(SqliteConnection c, string version)
    {
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Antwort(c).Version), Convert.FromHexString(version)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Equipments wurden zwischenzeitlich geändert. Bitte neu laden.");
    }
    private static void PruefeVersionEingabe(string? v)
    { if (string.IsNullOrWhiteSpace(v) || v.Length != 64 || !v.All(Uri.IsHexDigit)) throw Ungueltig("Versionskennung fehlt oder ist ungültig."); }
    private static void PruefeAnfrage(EquipmentSpeichernAnfrage? a)
    { if (a is null || a.Werte is null) throw Ungueltig(); PruefeVersionEingabe(a.Version); }
    private static void PruefeId(long id) { if (id <= 0) throw Ungueltig("Equipmentkennung muss positiv sein."); }
    private static void PruefeName(IEnumerable<Equipment> entries, string name)
    { if (entries.Any(e => string.Equals(e.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))) throw Ungueltig("Ein Equipment mit diesem Namen existiert bereits."); }
    private static EquipmentWerte PruefeWerte(SqliteConnection c, EquipmentWerte? w, Equipment? alt, IReadOnlyList<Kategorie>? categories)
    {
        if (w is null || w.Name is null || w.Geraetezuordnung is null || w.StandardKategorie is null) throw Ungueltig();
        var name = w.Name.Trim(); if (name.Length is < 1 or > 500 || name.Any(char.IsControl)) throw Ungueltig("Equipmentname muss 1 bis 500 Zeichen enthalten und darf keine Steuerzeichen enthalten.");
        if (!double.IsFinite(w.AnfangsdistanzKm) || w.AnfangsdistanzKm < 0) throw Ungueltig("Anfangsdistanz muss eine endliche, nichtnegative Zahl sein.");
        if (w.Geraetezuordnung.Length > 65536) throw Ungueltig("Gerätezuordnung ist zu lang.");
        if (w.AktivitaetId is <= 0) throw Ungueltig("Aktivitätskennung muss positiv sein.");
        if (w.AktivitaetId.HasValue && !AktivitaetenTabelle.Laden(c).Any(a => a.Id == w.AktivitaetId)) throw Ungueltig("Aktivität wurde nicht gefunden.");
        var cat = w.StandardKategorie.Trim(); if (cat.Length > 500 || cat.Any(char.IsControl)) throw Ungueltig("Standardkategorie ist ungültig.");
        if (cat.Length > 0)
        {
            var found = KategorienTabelle.Laden(c).FirstOrDefault(k => string.Equals(k.Name.Trim(), cat, StringComparison.OrdinalIgnoreCase));
            if (found is null && !(alt is not null && string.Equals(alt.StandardKategorie.Trim(), cat, StringComparison.OrdinalIgnoreCase))) throw Ungueltig("Standardkategorie wurde nicht gefunden.");
            if (found is not null) cat = found.Name;
            else cat = alt!.StandardKategorie;
        }
        var mapping = w.Geraetezuordnung;
        if (alt is null && mapping.Length != 0) throw Ungueltig("Neue Equipments dürfen keine Gerätekennungen festlegen.");
        if (alt is not null && mapping != alt.Geraetezuordnung)
        {
            var oldItems = GeraetezuordnungText.Lesen(alt.Geraetezuordnung); var newItems = GeraetezuordnungText.Lesen(mapping);
            if (newItems.Any(x => !oldItems.Contains(x))) throw Ungueltig("Gerätezuordnungen können nur entfernt, nicht hinzugefügt werden.");
            mapping = GeraetezuordnungText.Schreiben(newItems);
        }
        return w with { Name = name, StandardKategorie = cat, Geraetezuordnung = mapping };
    }
    private static Equipment AusModell(EquipmentWerte w, long id) => new() { Id = id, Name = w.Name, AktivitaetId = w.AktivitaetId,
        Geraetezuordnung = w.Geraetezuordnung, StandardKategorie = w.StandardKategorie, IstStandard = w.IstStandard,
        Ausgemustert = w.Ausgemustert, Anschaffungsdatum = w.Anschaffungsdatum, AnfangsdistanzKm = w.AnfangsdistanzKm };
    private static StammdatenException Ungueltig(string text = "Equipmentanforderung ist ungültig.") => new(StammdatenFehlerart.Ungueltig, text);
}
