using System.Globalization;
using System.Security.Cryptography;
using AndyTours.Application.Stammdaten;

namespace AndyTours.Import.Garmin;

public sealed partial class GarminImportPruefung
{
    private async Task<string> LegeSnapshotAb(Guid kandidatId, long garminId, FitSnapshot fit, CancellationToken cancellationToken)
    {
        if (kandidatId == Guid.Empty || garminId <= 0) throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Ablage ungültig.");
        cancellationToken.ThrowIfCancellationRequested();
        var relative = FormattableString.Invariant($"{fit.Aufzeichnung.Datum.Year:D4}/garmin-{garminId}-{kandidatId:N}.fit");
        var directory = Path.Combine(_datenordner, relative.Split('/')[0]);
        var finalPath = Path.Combine(directory, relative.Split('/')[1]);
        var temporary = Path.Combine(directory, ".garmin-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            Directory.CreateDirectory(directory);
            if (File.Exists(finalPath))
            {
                await VerifyExisting(finalPath, fit.DateiHash, cancellationToken).ConfigureAwait(false);
                return relative;
            }
            await using (var input = new FileStream(fit.TempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await GarminPersonalClient.KopiereBegrenztAsync(input, output, 128L * 1024 * 1024, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Move(temporary, finalPath);
            }
            catch (IOException) when (File.Exists(finalPath))
            {
                await VerifyExisting(finalPath, fit.DateiHash, cancellationToken).ConfigureAwait(false);
            }
            return relative;
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Ablage ungültig.");
        }
        finally { TryDelete(temporary); }
    }

    private static async Task VerifyExisting(string path, string expectedHash, CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > 128L * 1024 * 1024) throw new InvalidDataException();
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(expectedHash)))
                throw new StammdatenException(StammdatenFehlerart.Konflikt, "FIT-Datei ist bereits mit anderem Inhalt abgelegt.");
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Ablage ungültig.");
        }
    }
}
