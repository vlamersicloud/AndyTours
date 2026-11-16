using System.Security.Cryptography;
using AndyTours.Application.Import;
using AndyTours.Application.Stammdaten;

namespace AndyTours.Import.Dateien;

public sealed partial class FitUploadDienst
{
    public async Task<AndyTours.Contracts.V1.FitUploadAntwort> Hochladen(
        Guid uploadId,
        string dateiname,
        Stream inhalt,
        CancellationToken cancellationToken)
    {
        if (uploadId == Guid.Empty) throw Invalid("FIT-Upload ungültig.");
        if (inhalt is null || !inhalt.CanRead) throw Invalid("FIT-Upload ungültig.");
        dateiname = FitUploadRegeln.Dateiname(dateiname);

        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.Combine(_datenordner, "FitUploads", uploadId.ToString("N"));
        var destination = Path.Combine(directory, "original.fit");
        var temporary = Path.Combine(directory, ".upload-" + Guid.NewGuid().ToString("N") + ".tmp");
        var ownsTemporary = false;
        try
        {
            Directory.CreateDirectory(directory);
            await using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            ownsTemporary = true;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long total = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await inhalt.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                total += read;
                if (total > FitUploadRegeln.MaxDateiBytes) throw new FitUploadZuGrossException();
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            if (total == 0) throw Invalid("FIT-Datei ist leer.");
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            var contentHash = Convert.ToHexString(hash.GetHashAndReset());
            await output.DisposeAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.Move(temporary, destination);
                ownsTemporary = false;
            }
            catch (IOException) when (File.Exists(destination))
            {
                await VerifyExisting(destination, contentHash, total, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await _speicher.Registrieren(uploadId, dateiname, contentHash, total, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw; }
        catch (FitUploadZuGrossException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw Invalid("FIT-Upload konnte nicht gespeichert werden.");
        }
        finally
        {
            if (ownsTemporary) TryDelete(temporary);
        }
    }

    private static async Task VerifyExisting(string path, string expectedHash, long expectedBytes, CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length != expectedBytes || info.Length > FitUploadRegeln.MaxDateiBytes)
                throw new StammdatenException(StammdatenFehlerart.Konflikt, "FIT-Uploadkennung ist bereits mit anderem Inhalt belegt.");
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actualHash), Convert.FromHexString(expectedHash)))
                throw new StammdatenException(StammdatenFehlerart.Konflikt, "FIT-Uploadkennung ist bereits mit anderem Inhalt belegt.");
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw Invalid("FIT-Upload konnte nicht geprüft werden.");
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static StammdatenException Invalid(string message) =>
        new(StammdatenFehlerart.Ungueltig, message);
}
