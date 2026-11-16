using System.Security.Cryptography;
using AndyTours.Application.Import;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Import.Dateien;

public sealed partial class FitUploadDienst : IFitUploads
{
    public async Task<FitUploadUebernahmeAntwort> Uebernehmen(
        FitUploadUebernahmeAnfrage anfrage,
        CancellationToken cancellationToken)
    {
        if (anfrage is null) throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Übernahmeanfrage ungültig.");
        var receipt = await _speicher.Quittung(anfrage, cancellationToken).ConfigureAwait(false);
        if (receipt is not null) return receipt;

        var metadata = await _speicher.Pruefbestand(
            anfrage.Auswahl.Select(x => x.UploadId).ToArray(), cancellationToken).ConfigureAwait(false);
        if (!SameHash(metadata.Version, anfrage.Version))
            throw Conflict("FIT-Uploadauswahl wurde zwischenzeitlich geändert.");

        var uploads = metadata.Uploads.ToDictionary(x => x.UploadId);
        var liveTourIds = metadata.Bestand.Trainingseinheiten.Select(x => x.Id).ToHashSet();
        var snapshots = new Dictionary<Guid, FitDateiSnapshot>();
        try
        {
            foreach (var choice in anfrage.Auswahl)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var upload = uploads[choice.UploadId];
                var alreadyLinked = upload.Status == FitUploadStatus.Importiert &&
                    upload.TourId is long tourId && liveTourIds.Contains(tourId);
                if (!choice.Uebernehmen || alreadyLinked) continue;

                var directory = Path.Combine(_datenordner, "FitUploads", upload.UploadId.ToString("N"));
                var source = Path.Combine(directory, "original.fit");
                FitDateiSnapshot fit;
                try
                {
                    fit = await _fitDateien.Lesen(source, directory, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (StammdatenException) { throw Conflict("FIT-Datei wurde seit der Prüfung geändert oder ist ungültig."); }

                snapshots.Add(choice.UploadId, fit);
                long length;
                try { length = new FileInfo(fit.TempPath).Length; }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    throw Conflict("FIT-Datei konnte nicht geprüft werden.");
                }
                if (length != upload.Byteanzahl || !SameHash(fit.DateiHash, upload.InhaltHash))
                {
                    throw Conflict("FIT-Datei wurde seit der Prüfung geändert.");
                }
                if (!SameHash(fit.Version, choice.DateiVersion))
                {
                    throw Conflict("FIT-Datei- oder Zeitzonenprüfung ist veraltet.");
                }
            }

            var preparations = new List<FitUploadVorbereitung>(snapshots.Count);
            foreach (var (uploadId, fit) in snapshots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = FormattableString.Invariant($"{fit.Aufzeichnung.Datum.Year:D4}/upload-{uploadId:N}.fit");
                var storedPath = await _fitDateien.UnveraenderlichAblegen(relative, fit, cancellationToken).ConfigureAwait(false);
                preparations.Add(new FitUploadVorbereitung(uploadId, fit.Aufzeichnung, storedPath));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await _speicher.Uebernehmen(anfrage, preparations, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            foreach (var snapshot in snapshots.Values) snapshot.Dispose();
        }
    }

    private static bool SameHash(string? left, string? right)
    {
        if (left is not { Length: 64 } || right is not { Length: 64 } ||
            !left.All(Uri.IsHexDigit) || !right.All(Uri.IsHexDigit))
            return false;
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right));
    }

    private static StammdatenException Conflict(string message) =>
        new(StammdatenFehlerart.Konflikt, message);
}
