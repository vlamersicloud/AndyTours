using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Versionierte atomare SQLite-Verwaltung von Wartungen und Alarmen.</summary>
public sealed class SqliteWartungsverwaltung(string datenbankpfad) : IWartungsverwaltung
{
    private SqliteConnection Oeffnen() => OeffneDatenbank.Ausfuehren(datenbankpfad);

    public Task<WartungsuebersichtAntwort> Laden(Wartungsfilter filter, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (filter is null) throw Invalid();
        if (filter.AktivitaetId is <= 0 || filter.EquipmentId is <= 0) throw Invalid("Filterkennungen müssen positiv sein.");
        using var c = Oeffnen();
        using var tx = c.BeginTransaction(deferred: true);
        var data = DatenbestandTabellen.Laden(c);
        if (filter.AktivitaetId is long aid && !data.Aktivitaeten.Any(x => x.Id == aid)) throw NotFound("Aktivität wurde nicht gefunden.");
        if (filter.EquipmentId is long eid && !data.Equipments.Any(x => x.Id == eid)) throw NotFound("Equipment wurde nicht gefunden.");
        if (filter.EquipmentId is long selectedId && filter.AktivitaetId is long activityId &&
            !Wartungsaktivitaet.Passt(data.Equipments.Single(e => e.Id == selectedId), activityId))
            throw Invalid("Equipment gehört nicht zur gefilterten Aktivität.");
        var raw = Raw(data);
        var gespeicherteTaetigkeiten = AuswahllisteEinstellung.Laden(c, Auswahlliste.Taetigkeit);
        var grundlage = gespeicherteTaetigkeiten ?? ErmittleNamensliste.Ausfuehren(data.Wartungen.Select(w => w.Taetigkeit));
        var taetigkeiten = OrdneTaetigkeitenNachHaeufigkeit.Ausfuehren(grundlage, data.Wartungen);
        var rows = ErstelleWartungszeilen.Ausfuehren(data, filter.EquipmentId, filter.AktivitaetId)
            .Select(x => new WartungszeileAntwort(x.Id, x.Datum, x.Rad, x.KmStand, x.KmSeitLetzter, x.TageSeitLetzter, x.Taetigkeit, x.Kommentar)).ToArray();
        var alarms = ErstelleAlarmzeilen.Ausfuehren(data, filter.EquipmentId, filter.AktivitaetId)
            .Select(x => new AlarmzeileAntwort(x.Id, x.Rad, x.Taetigkeit, x.IntervallKm, x.GefahrenKm, x.VerbleibendKm, x.Zustand switch
            {
                Alarmzustand.Faellig => Wartungsalarmzustand.Faellig,
                Alarmzustand.BaldFaellig => Wartungsalarmzustand.BaldFaellig,
                Alarmzustand.Normal => Wartungsalarmzustand.Normal,
                Alarmzustand.Ausgeschaltet => Wartungsalarmzustand.Ausgeschaltet,
                _ => throw new InvalidOperationException("Unbekannter Alarmzustand.")
            })).ToArray();
        ct.ThrowIfCancellationRequested(); tx.Commit();
        return Task.FromResult(new WartungsuebersichtAntwort(raw, rows, alarms, taetigkeiten));
    }

    public Task<WartungsbestandAntwort> WartungAnlegen(WartungSpeichernAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (anfrage is null) throw Invalid();
        ValidateRequest(anfrage.Version, false);
        ValidateWartung(anfrage.Datum, anfrage.EquipmentId, anfrage.Taetigkeit, anfrage.Kommentar);
        using var c = Oeffnen();
        using var tx = c.BeginTransaction(deferred: false);
        CheckVersion(c, anfrage.Version);
        var data = DatenbestandTabellen.Laden(c); ValidateEquipment(data, anfrage.EquipmentId, null);
        CheckDuplicate(data, null, anfrage.EquipmentId, anfrage.Datum, anfrage.Taetigkeit);
        ct.ThrowIfCancellationRequested();
        WartungenTabelle.Einfuegen(c, tx, new Wartung { Datum = anfrage.Datum, EquipmentId = anfrage.EquipmentId,
            Taetigkeit = anfrage.Taetigkeit.Trim(), Kommentar = anfrage.Kommentar.Trim(), Typ = WartungsTyp.Wartung, AlarmKm = 0, Aktiv = true });
        return Finish(c, tx, ct);
    }

    public Task<WartungsbestandAntwort> WartungAendern(long id, WartungSpeichernAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        CheckId(id);
        if (anfrage is null) throw Invalid();
        ValidateRequest(anfrage.Version, false);
        ValidateWartung(anfrage.Datum, anfrage.EquipmentId, anfrage.Taetigkeit, anfrage.Kommentar);
        using var c = Oeffnen();
        using var tx = c.BeginTransaction(deferred: false);
        CheckVersion(c, anfrage.Version);
        var data = DatenbestandTabellen.Laden(c); var old = data.Wartungen.SingleOrDefault(w => w.Id == id) ?? throw NotFound("Wartung wurde nicht gefunden.");
        if (old.Typ != WartungsTyp.Wartung) throw Invalid("Der Eintrag ist keine Wartung.");
        ValidateEquipment(data, anfrage.EquipmentId, old.EquipmentId); CheckDuplicate(data, id, anfrage.EquipmentId, anfrage.Datum, anfrage.Taetigkeit);
        old.Datum = anfrage.Datum; old.EquipmentId = anfrage.EquipmentId; old.Taetigkeit = anfrage.Taetigkeit.Trim(); old.Kommentar = anfrage.Kommentar.Trim();
        ct.ThrowIfCancellationRequested();
        WartungenTabelle.Aendern(c, tx, old);
        return Finish(c, tx, ct);
    }

    public Task<WartungsbestandAntwort> AlarmAnlegen(AlarmSpeichernAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (anfrage is null) throw Invalid();
        ValidateRequest(anfrage.Version, false);
        ValidateAlarm(anfrage.EquipmentId, anfrage.Taetigkeit, anfrage.IntervallKm);
        using var c = Oeffnen();
        using var tx = c.BeginTransaction(deferred: false);
        CheckVersion(c, anfrage.Version);
        var data = DatenbestandTabellen.Laden(c); ValidateEquipment(data, anfrage.EquipmentId, null); CheckAlarmDuplicate(data, null, anfrage.EquipmentId, anfrage.Taetigkeit);
        ct.ThrowIfCancellationRequested();
        WartungenTabelle.Einfuegen(c, tx, new Wartung { Datum = DateOnly.FromDateTime(DateTime.Today), EquipmentId = anfrage.EquipmentId,
            Taetigkeit = anfrage.Taetigkeit.Trim(), Kommentar = "", Typ = WartungsTyp.Alarm, AlarmKm = anfrage.IntervallKm, Aktiv = anfrage.Eingeschaltet });
        return Finish(c, tx, ct);
    }

    public Task<WartungsbestandAntwort> AlarmAendern(long id, AlarmSpeichernAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        CheckId(id);
        if (anfrage is null) throw Invalid();
        ValidateRequest(anfrage.Version, false);
        ValidateAlarm(anfrage.EquipmentId, anfrage.Taetigkeit, anfrage.IntervallKm);
        using var c = Oeffnen();
        using var tx = c.BeginTransaction(deferred: false);
        CheckVersion(c, anfrage.Version);
        var data = DatenbestandTabellen.Laden(c); var old = data.Wartungen.SingleOrDefault(w => w.Id == id) ?? throw NotFound("Alarm wurde nicht gefunden.");
        if (old.Typ != WartungsTyp.Alarm) throw Invalid("Der Eintrag ist kein Alarm.");
        ValidateEquipment(data, anfrage.EquipmentId, old.EquipmentId); CheckAlarmDuplicate(data, id, anfrage.EquipmentId, anfrage.Taetigkeit);
        old.EquipmentId = anfrage.EquipmentId; old.Taetigkeit = anfrage.Taetigkeit.Trim(); old.AlarmKm = anfrage.IntervallKm; old.Aktiv = anfrage.Eingeschaltet;
        ct.ThrowIfCancellationRequested();
        WartungenTabelle.Aendern(c, tx, old);
        return Finish(c, tx, ct);
    }

    public Task<WartungsbestandAntwort> Loeschen(WartungenAuswahlAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); ValidateSelection(anfrage);
        using var c = Oeffnen();
        using var tx = c.BeginTransaction(deferred: false);
        CheckVersion(c, anfrage.Version);
        var data = DatenbestandTabellen.Laden(c); EnsureSelectionExists(data, anfrage.Ids);
        ct.ThrowIfCancellationRequested();
        WartungenTabelle.Loeschen(c, tx, anfrage.Ids);
        return Finish(c, tx, ct);
    }

    public Task<WartungsbestandAntwort> AlarmeUmschalten(WartungenAuswahlAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); ValidateSelection(anfrage);
        using var c = Oeffnen();
        using var tx = c.BeginTransaction(deferred: false);
        CheckVersion(c, anfrage.Version);
        var data = DatenbestandTabellen.Laden(c); var selected = anfrage.Ids.Select(id => data.Wartungen.SingleOrDefault(w => w.Id == id) ?? throw NotFound("Ausgewählter Eintrag wurde nicht gefunden.")).ToArray();
        if (selected.Any(w => w.Typ != WartungsTyp.Alarm)) throw Invalid("Die Auswahl darf nur Alarme enthalten.");
        var active = !selected.Any(w => w.Aktiv);
        foreach (var alarm in selected)
        {
            ct.ThrowIfCancellationRequested();
            alarm.Aktiv = active;
            WartungenTabelle.Aendern(c, tx, alarm);
        }
        return Finish(c, tx, ct);
    }

    private static Task<WartungsbestandAntwort> Finish(SqliteConnection c, SqliteTransaction tx, CancellationToken ct)
    { var result = Raw(DatenbestandTabellen.Laden(c)); ct.ThrowIfCancellationRequested(); tx.Commit(); return Task.FromResult(result); }
    private static WartungsbestandAntwort Raw(Datenbestand data)
    {
        var entries = data.Wartungen.OrderBy(w => w.Id).Select(w => new WartungsdatensatzAntwort(w.Id, w.Datum, w.EquipmentId, w.Taetigkeit, w.Kommentar,
            w.Typ switch { WartungsTyp.Wartung => Wartungsart.Wartung, WartungsTyp.Alarm => Wartungsart.Alarm, _ => throw new InvalidOperationException("Unbekannter Wartungstyp.") }, w.AlarmKm, w.Aktiv)).ToArray();
        return new WartungsbestandAntwort(Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(entries))), entries);
    }
    private static void CheckVersion(SqliteConnection c, string version)
    { if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Raw(DatenbestandTabellen.Laden(c)).Version), Convert.FromHexString(version))) throw new StammdatenException(StammdatenFehlerart.Konflikt, "Wartungen wurden zwischenzeitlich geändert. Bitte neu laden."); }
    private static void ValidateRequest(string? version, bool absent)
    { if (absent || string.IsNullOrWhiteSpace(version) || version.Length != 64 || !version.All(Uri.IsHexDigit)) throw Invalid("Anfrage oder Versionskennung ist ungültig."); }
    private static void ValidateSelection(WartungenAuswahlAnfrage? request)
    { ValidateRequest(request?.Version, request is null); if (request!.Ids is null || request.Ids.Count is < 1 or > 1000 || request.Ids.Any(x => x <= 0) || request.Ids.Distinct().Count() != request.Ids.Count) throw Invalid("Auswahl muss 1 bis 1000 verschiedene positive IDs enthalten."); }
    private static void EnsureSelectionExists(Datenbestand data, IReadOnlyList<long> ids)
    { if (!ids.All(id => data.Wartungen.Any(w => w.Id == id))) throw NotFound("Mindestens ein ausgewählter Eintrag wurde nicht gefunden."); }
    private static void CheckId(long id) { if (id <= 0) throw Invalid("ID muss positiv sein."); }
    private static void ValidateText(string? value, string field, int max, bool required, bool multiline = false)
    {
        if (value is null || value.Trim().Length > max || (required && value.Trim().Length == 0) ||
            value.Any(c => char.IsControl(c) && !(multiline && c is '\r' or '\n' or '\t')))
            throw Invalid($"{field} ist ungültig.");
    }
    private static void ValidateWartung(DateOnly date, long? equipmentId, string? task, string? comment)
    { if (date > DateOnly.FromDateTime(DateTime.Today)) throw Invalid("Wartungsdatum darf nicht in der Zukunft liegen."); if (equipmentId is <= 0) throw Invalid("Equipment-ID muss positiv sein."); ValidateText(task, "Tätigkeit", 500, true); ValidateText(comment, "Kommentar", 20000, false, multiline: true); }
    private static void ValidateAlarm(long equipmentId, string? task, int interval)
    { if (equipmentId <= 0 || interval <= 0) throw Invalid("Equipment-ID und Intervall müssen positiv sein."); ValidateText(task, "Tätigkeit", 500, true); }
    private static void ValidateEquipment(Datenbestand data, long? id, long? allowRetiredId)
    {
        if (id is null) throw Invalid("Equipment muss ausgewählt sein.");
        var equipment = data.Equipments.SingleOrDefault(e => e.Id == id.Value) ?? throw NotFound("Equipment wurde nicht gefunden.");
        if (equipment.Ausgemustert && id != allowRetiredId)
            throw Invalid("Für ausgemusterte Equipments können keine neuen Einträge angelegt werden.");
    }
    private static void CheckDuplicate(Datenbestand data, long? except, long? equipment, DateOnly date, string task)
    { if (data.Wartungen.Any(w => w.Id != except && w.Typ == WartungsTyp.Wartung && w.EquipmentId == equipment && w.Datum == date && VergleicheNamen.Ausfuehren(w.Taetigkeit, task))) throw Invalid("Eine gleichartige Wartung für dieses Equipment und Datum existiert bereits."); }
    private static void CheckAlarmDuplicate(Datenbestand data, long? except, long equipment, string task)
    { if (data.Wartungen.Any(w => w.Id != except && w.Typ == WartungsTyp.Alarm && w.EquipmentId == equipment && VergleicheNamen.Ausfuehren(w.Taetigkeit, task))) throw Invalid("Ein Alarm mit dieser Tätigkeit existiert für das Equipment bereits."); }
    private static StammdatenException Invalid(string text = "Wartungsanforderung ist ungültig.") => new(StammdatenFehlerart.Ungueltig, text);
    private static StammdatenException NotFound(string text) => new(StammdatenFehlerart.NichtGefunden, text);
}
