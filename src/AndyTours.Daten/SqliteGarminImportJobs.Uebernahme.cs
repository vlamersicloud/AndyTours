using System.Security.Cryptography;
using System.Globalization;
using System.Text.Json;
using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteGarminImportJobs : IGarminImportPruefspeicher
{
    public Task<GarminImportUebernahmeAntwort> Uebernehmen(Guid jobId, GarminImportUebernahmeAnfrage anfrage, IReadOnlyList<GarminImportVorbereitung> vorbereitet, CancellationToken cancellationToken)
    {
        ValidateId(jobId);
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);
        var replay = ReadUebernahmeQuittung(db, tx, jobId, anfrage);
        if (replay is not null) return Task.FromResult(replay);
        var hash = ValidateUebernahmeAnfrage(anfrage);
        var job = ReadJob(db, jobId) ?? throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Garmin-Import wurde nicht gefunden.");
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(job.Version), Convert.FromHexString(anfrage.Version))) throw Konflikt("Garmin-Import wurde zwischenzeitlich geändert.");
        if (job.Status is GarminImportJobStatus.Wartend or GarminImportJobStatus.Laeuft) throw Konflikt("Garmin-Import ist noch nicht prüfbereit.");
        var bestand = DatenbestandTabellen.Laden(db);
        var known = ReadCandidates(db, tx, jobId).Select(x => x.Kandidat).ToDictionary(x => x.KandidatId);
        var selected = anfrage.Auswahl.ToDictionary(x => x.KandidatId);
        if (selected.Keys.Any(id => !known.TryGetValue(id, out var c) || c.Status != GarminImportKandidatStatus.FitBereit)) throw Konflikt("Garmin-Kandidat ist nicht mehr prüfbereit.");
        if (vorbereitet is null || vorbereitet.Any(p => p is null || p.Aufzeichnung is null)) throw Ungueltig("FIT-Vorbereitung ungültig.");
        var todo = anfrage.Auswahl.Where(x => x.Uebernehmen).ToArray();
        if (vorbereitet.Count != todo.Length || vorbereitet.Select(x => x.KandidatId).Distinct().Count() != vorbereitet.Count || todo.Any(x => !vorbereitet.Any(p => p.KandidatId == x.KandidatId))) throw Ungueltig("FIT-Vorbereitung ungültig.");
        var main = bestand.Aktivitaeten.FirstOrDefault(x => x.IstHaupt)?.Id;
        if (main is null) throw Ungueltig("Hauptaktivität fehlt.");
        var liveTours = bestand.Trainingseinheiten;
        var mapping = GarminImportZuordnung.Laden(db, liveTours.Select(x => x.Id).ToHashSet());
        var results = new List<GarminImportUebernahmeZeile>();
        foreach (var choice in anfrage.Auswahl)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = known[choice.KandidatId];
            var status = GarminImportKandidatStatus.Uebersprungen;
            long? tourId = null;
            bool imported = false;
            string? note = "auswahl_uebersprungen";
            if (choice.Uebernehmen)
            {
                var prep = vorbereitet.Single(x => x.KandidatId == choice.KandidatId);
                var activity = candidate.GarminAktivitaetId;
                if (prep.Aufzeichnung.DistanzKm < 0 || double.IsNaN(prep.Aufzeichnung.DistanzKm) || double.IsInfinity(prep.Aufzeichnung.DistanzKm) || prep.Aufzeichnung.Fahrzeit < TimeSpan.Zero) throw Ungueltig("FIT-Daten ungültig.");
                var expected = $"{prep.Aufzeichnung.Datum.Year:D4}/garmin-{activity}-{choice.KandidatId:N}.fit";
                if (!string.Equals(prep.RelativerPfad, expected, StringComparison.Ordinal)) throw Ungueltig("FIT-Pfad ungültig.");
                long? equipmentId = choice.EquipmentId;
                if (equipmentId is not null && !bestand.Equipments.Any(e => e.Id == equipmentId && !e.Ausgemustert)) throw Ungueltig("Equipment ungültig.");
                if (mapping.Contains(activity))
                {
                    var mapped = EinstellungTabelle.Lesen(db, "GarminConnect.Aktivitaet." + activity.ToString(CultureInfo.InvariantCulture));
                    if (!long.TryParse(mapped, NumberStyles.None, CultureInfo.InvariantCulture, out var existingTourId) || !liveTours.Any(t => t.Id == existingTourId))
                        throw Konflikt("Garmin-Zuordnung ist nicht mehr gültig.");
                    tourId = existingTourId;
                    status = GarminImportKandidatStatus.Importiert;
                    note = "bereits_importiert";
                }
                else if (IstTourVorhanden.Ausfuehren(liveTours, prep.Aufzeichnung.Datum, prep.Aufzeichnung.Startzeit)) { note = "tour_bereits_vorhanden"; }
                else
                {
                    var unit = ErstelleImportierteEinheit.Ausfuehren(bestand, prep.Aufzeichnung, equipmentId, expected);
                    cancellationToken.ThrowIfCancellationRequested();
                    var saved = TrainingseinheitenTabelle.Einfuegen(db, tx, unit, (connection, transaction, id) => GarminImportZuordnung.Speichern(connection, transaction, activity, id));
                    tourId = saved.Id;
                    unit.Id = saved.Id; unit.FahrerId = saved.FahrerId; liveTours.Add(unit);
                    if (bestand.Fahrer.All(f => f.Id != saved.FahrerId)) bestand.Fahrer.Add(new Fahrer { Id = saved.FahrerId });
                    var changes = MerkeRadkennungen.Ausfuehren(bestand, prep.Aufzeichnung, equipmentId);
                    foreach (var change in changes)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        EquipmentsTabelle.Aendern(db, tx, change);
                    }
                    UebernimmEquipment.Einsetzen(bestand, changes);
                    status = GarminImportKandidatStatus.Importiert;
                    imported = true;
                    note = null;
                }
            }
            var savedCandidate = candidate with { Status = status, Warncode = null };
            cancellationToken.ThrowIfCancellationRequested();
            UpdateCandidate(db, tx, jobId, job.Paketnummer, candidate.KandidatId, savedCandidate);
            results.Add(new GarminImportUebernahmeZeile(candidate.KandidatId, tourId, imported, note));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var remainingFit = ScalarLong(db, tx, "SELECT COUNT(*) FROM GarminImportKandidat WHERE JobId=$id AND Zustand=2;", ("$id", jobId.ToString("D")));
        var nextState = job.Status == GarminImportJobStatus.PaketBereit && !job.WeitereMoeglich && remainingFit == 0
            ? GarminImportJobStatus.Fertig : job.Status;
        using var update = db.CreateCommand();
        update.Transaction = tx;
        update.CommandText = "UPDATE GarminImportJob SET Zustand=$state,Revision=Revision+1,AktualisiertUtc=$now WHERE JobId=$job;";
        update.Parameters.AddWithValue("$state", (int)nextState);
        update.Parameters.AddWithValue("$now", Format(_zeit.GetUtcNow()));
        update.Parameters.AddWithValue("$job", jobId.ToString("D"));
        update.ExecuteNonQuery();
        var updated = ReadJob(db, jobId)!;
        var response = new GarminImportUebernahmeAntwort(updated, results);
        using var receipt = db.CreateCommand();
        receipt.Transaction = tx;
        receipt.CommandText = "INSERT INTO GarminImportSteuerung(JobId,AnfrageId,Aktion,AnfrageHash,AntwortJson) VALUES($job,$request,'Uebernehmen',$hash,$response);";
        receipt.Parameters.AddWithValue("$job", jobId.ToString("D"));
        receipt.Parameters.AddWithValue("$request", anfrage.AnfrageId.ToString("D"));
        receipt.Parameters.AddWithValue("$hash", hash);
        receipt.Parameters.AddWithValue("$response", JsonSerializer.Serialize(response));
        cancellationToken.ThrowIfCancellationRequested();
        receipt.ExecuteNonQuery();
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(response);
    }
}
