using System.Security.Cryptography;
using System.Globalization;
using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.Import.Garmin;

public sealed partial class GarminImportPruefung : IGarminImportPruefung
{
    public async Task<GarminImportUebernahmeAntwort> Uebernehmen(Guid jobId, GarminImportUebernahmeAnfrage anfrage, CancellationToken cancellationToken = default)
    {
        var receipt = await _speicher.Quittung(jobId, anfrage, cancellationToken).ConfigureAwait(false);
        if (receipt is not null) return receipt;
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = await _speicher.Pruefbestand(jobId, cancellationToken).ConfigureAwait(false);
        if (!ValidVersion(anfrage.Version) || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(snapshot.Auftrag.Version), Convert.FromHexString(anfrage.Version)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Garmin-Import wurde zwischenzeitlich geändert.");
        var candidates = snapshot.Kandidaten.ToDictionary(x => x.KandidatId);
        var prepared = new List<GarminImportVorbereitung>();
        foreach (var choice in anfrage.Auswahl)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!candidates.TryGetValue(choice.KandidatId, out var candidate) || candidate.Status != GarminImportKandidatStatus.FitBereit)
                throw new StammdatenException(StammdatenFehlerart.Konflikt, "Garmin-Kandidat ist nicht mehr prüfbereit.");
            if (!choice.Uebernehmen) continue;
            using var fit = await LeseSnapshot(jobId, candidate.KandidatId, cancellationToken).ConfigureAwait(false);
            if (!ValidVersion(choice.DateiVersion) || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(fit.Version), Convert.FromHexString(choice.DateiVersion)))
                throw new StammdatenException(StammdatenFehlerart.Konflikt, "FIT-Datei wurde zwischenzeitlich geändert.");
            var relativePath = await LegeSnapshotAb(candidate.KandidatId, candidate.GarminAktivitaetId, fit, cancellationToken).ConfigureAwait(false);
            prepared.Add(new GarminImportVorbereitung(candidate.KandidatId, fit.Aufzeichnung, relativePath));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return await _speicher.Uebernehmen(jobId, anfrage, prepared, cancellationToken).ConfigureAwait(false);
    }

    private static bool ValidVersion(string? version) => version is { Length: 64 } && version.All(Uri.IsHexDigit);
}
