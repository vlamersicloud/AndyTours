using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;

namespace AndyTours.Import.Garmin;

public sealed partial class GarminImportPruefung
{
    public async Task<GarminImportPruefungAntwort> Laden(Guid jobId, CancellationToken cancellationToken = default)
    {
        var snapshot = await _speicher.Pruefbestand(jobId, cancellationToken).ConfigureAwait(false);
        var tours = snapshot.Bestand.Trainingseinheiten.ToList();
        var rows = new List<GarminImportPruefzeile>();
        foreach (var candidate in snapshot.Kandidaten.OrderBy(x => x.Datum).ThenBy(x => x.GarminAktivitaetId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var fit = await LeseSnapshot(jobId, candidate.KandidatId, cancellationToken).ConfigureAwait(false);
            var record = fit.Aufzeichnung;
            var recommendation = ErmittleImportEquipment.Ausfuehren(snapshot.Bestand, record);
            var duplicate = IstTourVorhanden.Ausfuehren(tours, record.Datum, record.Startzeit);
            if (!duplicate)
                tours.Add(new Trainingseinheit { Datum = record.Datum, Startzeit = record.Startzeit });
            rows.Add(new GarminImportPruefzeile(candidate.KandidatId, candidate.GarminAktivitaetId, fit.Version,
                record.Datum, record.Startzeit, checked((long)record.Fahrzeit.TotalSeconds), record.DistanzKm,
                record.Hoehenmeter, record.Geraet ?? string.Empty, record.Profil ?? string.Empty,
                recommendation?.Id, duplicate, duplicate ? "tour_bereits_vorhanden" : null));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var equipment = snapshot.Bestand.Equipments.Where(x => !x.Ausgemustert).Select(x => new EquipmentAntwort(
            x.Id, x.Name, x.AktivitaetId, x.Geraetezuordnung, x.StandardKategorie, x.IstStandard, x.Ausgemustert,
            x.Anschaffungsdatum, x.AnfangsdistanzKm)).ToArray();
        return new GarminImportPruefungAntwort(jobId, snapshot.Auftrag.Version, rows, equipment);
    }
}
