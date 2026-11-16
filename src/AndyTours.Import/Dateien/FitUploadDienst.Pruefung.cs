using System.Security.Cryptography;
using AndyTours.Application.Import;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;

namespace AndyTours.Import.Dateien;

public sealed partial class FitUploadDienst
{
    public async Task<FitUploadPruefungAntwort> Pruefen(
        FitUploadPruefanfrage anfrage,
        CancellationToken cancellationToken)
    {
        if (anfrage is null) throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Prüfanfrage ungültig.");
        var snapshot = await _speicher.Pruefbestand(anfrage.UploadIds, cancellationToken).ConfigureAwait(false);
        var tours = snapshot.Bestand.Trainingseinheiten.ToList();
        var rows = new List<FitUploadPruefzeile>();
        var errors = new List<FitUploadPrueffehler>();
        var validUploads = new List<AndyTours.Contracts.V1.FitUploadAntwort>();

        foreach (var upload in snapshot.Uploads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = Path.Combine(_datenordner, "FitUploads", upload.UploadId.ToString("N"));
            var source = Path.Combine(directory, "original.fit");
            try
            {
                using var fit = await _fitDateien.Lesen(source, directory, cancellationToken).ConfigureAwait(false);
                var length = new FileInfo(fit.TempPath).Length;
                if (length != upload.Byteanzahl ||
                    !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(fit.DateiHash), Convert.FromHexString(upload.InhaltHash)))
                {
                    errors.Add(new FitUploadPrueffehler(upload.UploadId, upload.Dateiname, "fit_ungueltig"));
                    continue;
                }

                var record = fit.Aufzeichnung;
                var recommendation = ErmittleImportEquipment.Ausfuehren(snapshot.Bestand, record);
                var duplicate = IstTourVorhanden.Ausfuehren(tours, record.Datum, record.Startzeit);
                if (!duplicate)
                    tours.Add(new Trainingseinheit { Datum = record.Datum, Startzeit = record.Startzeit });
                rows.Add(new FitUploadPruefzeile(upload.UploadId, fit.Version, record.Datum, record.Startzeit,
                    checked((long)record.Fahrzeit.TotalSeconds), record.DistanzKm, record.Hoehenmeter,
                    record.Geraet ?? string.Empty, record.Profil ?? string.Empty, recommendation?.Id,
                    duplicate, duplicate ? "tour_bereits_vorhanden" : null)
                {
                    Dateiname = upload.Dateiname
                });
                validUploads.Add(upload);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is StammdatenException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                errors.Add(new FitUploadPrueffehler(upload.UploadId, upload.Dateiname, "fit_ungueltig"));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var equipment = snapshot.Bestand.Equipments.Where(x => !x.Ausgemustert).Select(x => new EquipmentAntwort(
            x.Id, x.Name, x.AktivitaetId, x.Geraetezuordnung, x.StandardKategorie, x.IstStandard, x.Ausgemustert,
            x.Anschaffungsdatum, x.AnfangsdistanzKm)).ToArray();
        return new FitUploadPruefungAntwort(FitUploadVersionen.Bestand(validUploads), rows, equipment)
        {
            FallbackZeitzoneId = _fallbackZeitzone.Id,
            Fehler = errors
        };
    }
}
