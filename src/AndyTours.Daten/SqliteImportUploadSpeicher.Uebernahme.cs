using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Import;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

public sealed partial class SqliteImportUploadSpeicher : IImportUploadSpeicher
{
    public Task<FitUploadUebernahmeAntwort> Uebernehmen(
        FitUploadUebernahmeAnfrage anfrage,
        IReadOnlyList<FitUploadVorbereitung> vorbereitet,
        CancellationToken cancellationToken)
    {
        var requestHash = ValidateUebernahmeAnfrage(anfrage);
        if (vorbereitet is null || vorbereitet.Any(x => x is null))
            throw Invalid("FIT-Vorbereitung ungültig.");
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);

        var replay = ReadReceipt(db, tx, anfrage.AnfrageId, requestHash);
        if (replay is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            tx.Commit();
            return Task.FromResult(replay);
        }

        var uploads = anfrage.Auswahl.Select(x =>
            Read(db, tx, x.UploadId) ?? throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "FIT-Upload wurde nicht gefunden.")).ToArray();
        var currentSetVersion = FitUploadVersionen.Bestand(uploads);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(currentSetVersion), Convert.FromHexString(anfrage.Version)))
            throw Conflict("FIT-Uploadauswahl wurde zwischenzeitlich geändert.");

        var preparedById = vorbereitet.ToDictionary(x => x.UploadId);
        var selected = anfrage.Auswahl.Where(x => x.Uebernehmen).ToArray();
        if (preparedById.Count != selected.Length || selected.Any(x => !preparedById.ContainsKey(x.UploadId)) ||
            vorbereitet.Any(x => !selected.Any(s => s.UploadId == x.UploadId)))
            throw Invalid("FIT-Vorbereitung ungültig.");
        foreach (var choice in anfrage.Auswahl)
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(uploads.Single(x => x.UploadId == choice.UploadId).Version),
                    Convert.FromHexString(choice.DateiVersion)))
                throw Conflict("FIT-Datei wurde zwischenzeitlich geändert.");

        var bestand = DatenbestandTabellen.Laden(db);
        var mainActivity = bestand.Aktivitaeten.FirstOrDefault(x => x.IstHaupt);
        var results = new List<FitUploadUebernahmeZeile>(anfrage.Auswahl.Count);
        var changed = new Dictionary<Guid, (FitUploadStatus Status, long? TourId)>();
        var liveTours = bestand.Trainingseinheiten;

        foreach (var choice in anfrage.Auswahl)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = uploads.Single(x => x.UploadId == choice.UploadId);
            if (current.Status == FitUploadStatus.Importiert && current.TourId is long linkedTour && liveTours.Any(x => x.Id == linkedTour))
            {
                changed[choice.UploadId] = (FitUploadStatus.Importiert, linkedTour);
                results.Add(new FitUploadUebernahmeZeile(choice.UploadId, linkedTour, false, "bereits_importiert"));
                continue;
            }
            if (!choice.Uebernehmen)
            {
                changed[choice.UploadId] = (FitUploadStatus.Uebersprungen, null);
                results.Add(new FitUploadUebernahmeZeile(choice.UploadId, null, false, "auswahl_uebersprungen"));
                continue;
            }
            if (mainActivity is null) throw Invalid("Hauptaktivität fehlt.");
            var prep = preparedById[choice.UploadId];
            if (prep.Aufzeichnung is null || !double.IsFinite(prep.Aufzeichnung.DistanzKm) || prep.Aufzeichnung.DistanzKm < 0 ||
                prep.Aufzeichnung.Fahrzeit < TimeSpan.Zero || prep.Aufzeichnung.Fahrzeit.Ticks % TimeSpan.TicksPerSecond != 0 ||
                !double.IsFinite(prep.Aufzeichnung.DistanzKm))
                throw Invalid("FIT-Daten ungültig.");
            if (choice.EquipmentId is long equipmentId && !bestand.Equipments.Any(x => x.Id == equipmentId && !x.Ausgemustert))
                throw Invalid("Equipment ungültig.");
            var expectedPath = FormattableString.Invariant($"{prep.Aufzeichnung.Datum.Year:D4}/upload-{choice.UploadId:N}.fit");
            if (!string.Equals(prep.RelativerPfad, expectedPath, StringComparison.Ordinal))
                throw Invalid("FIT-Pfad ungültig.");
            if (IstTourVorhanden.Ausfuehren(liveTours, prep.Aufzeichnung.Datum, prep.Aufzeichnung.Startzeit))
            {
                changed[choice.UploadId] = (FitUploadStatus.Uebersprungen, null);
                results.Add(new FitUploadUebernahmeZeile(choice.UploadId, null, false, "tour_bereits_vorhanden"));
                continue;
            }

            var unit = ErstelleImportierteEinheit.Ausfuehren(bestand, prep.Aufzeichnung, choice.EquipmentId, expectedPath);
            cancellationToken.ThrowIfCancellationRequested();
            var saved = TrainingseinheitenTabelle.Einfuegen(db, tx, unit);
            unit.Id = saved.Id;
            unit.FahrerId = saved.FahrerId;
            liveTours.Add(unit);
            if (bestand.Fahrer.All(x => x.Id != saved.FahrerId)) bestand.Fahrer.Add(new Fahrer { Id = saved.FahrerId });
            var changes = MerkeRadkennungen.Ausfuehren(bestand, prep.Aufzeichnung, choice.EquipmentId);
            foreach (var change in changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EquipmentsTabelle.Aendern(db, tx, change);
            }
            UebernimmEquipment.Einsetzen(bestand, changes);
            changed[choice.UploadId] = (FitUploadStatus.Importiert, saved.Id);
            results.Add(new FitUploadUebernahmeZeile(choice.UploadId, saved.Id, true, null));
        }

        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        foreach (var (id, update) in changed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var command = db.CreateCommand();
            command.Transaction = tx;
            command.CommandText = "UPDATE FitUpload SET Zustand=@state,TourId=@tour,Revision=Revision+1,AktualisiertUtc=@now WHERE UploadId=@id;";
            command.Parameters.AddWithValue("@state", (int)update.Status);
            command.Parameters.AddWithValue("@tour", update.TourId is long tourId ? tourId : DBNull.Value);
            command.Parameters.AddWithValue("@now", now);
            command.Parameters.AddWithValue("@id", id.ToString("D"));
            if (command.ExecuteNonQuery() != 1) throw Conflict("FIT-Upload wurde zwischenzeitlich geändert.");
        }

        var finalUploads = anfrage.Auswahl.Select(x => Read(db, tx, x.UploadId)!).ToArray();
        var response = new FitUploadUebernahmeAntwort(finalUploads, results);
        using (var receipt = db.CreateCommand())
        {
            receipt.Transaction = tx;
            receipt.CommandText = "INSERT INTO FitUploadQuittung(AnfrageId,AnfrageHash,AntwortJson) VALUES(@id,@hash,@json);";
            receipt.Parameters.AddWithValue("@id", anfrage.AnfrageId.ToString("D"));
            receipt.Parameters.AddWithValue("@hash", requestHash);
            receipt.Parameters.AddWithValue("@json", JsonSerializer.Serialize(response));
            cancellationToken.ThrowIfCancellationRequested();
            receipt.ExecuteNonQuery();
        }
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(response);
    }

    private static FitUploadUebernahmeAntwort? ReadReceipt(SqliteConnection db, SqliteTransaction tx, Guid requestId, string requestHash)
    {
        using var command = db.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT AnfrageHash,AntwortJson FROM FitUploadQuittung WHERE AnfrageId=@id;";
        command.Parameters.AddWithValue("@id", requestId.ToString("D"));
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(reader.GetString(0)), Convert.FromHexString(requestHash)))
            throw Conflict("FIT-Importanfrage wurde mit abweichendem Inhalt wiederverwendet.");
        return JsonSerializer.Deserialize<FitUploadUebernahmeAntwort>(reader.GetString(1))
            ?? throw Conflict("FIT-Importquittung ungültig.");
    }

}
