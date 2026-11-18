namespace AndyTours.App.Funktionen;

using System.IO;
using System.Security.Cryptography;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

/// <summary>Writes a verified download to an owned same-directory temporary file and publishes without overwrite.</summary>
public static class SicherungsdownloadDatei
{
    public static async Task Schreiben(
        ISicherungsdownloadClient client, SicherungAntwort selected, string targetPath,
        Func<bool> stillCurrent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(stillCurrent);
        cancellationToken.ThrowIfCancellationRequested();
        if (!WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig(selected) ||
            string.IsNullOrWhiteSpace(targetPath) || !Path.IsPathFullyQualified(targetPath))
            throw new InvalidDataException("Downloadziel oder Katalogeintrag ist ungültig.");

        var target = Path.GetFullPath(targetPath);
        if (File.Exists(target))
            throw new IOException("Die Zieldatei existiert bereits und wird nicht überschrieben.");
        var directory = Path.GetDirectoryName(target) ?? throw new IOException("Downloadzielordner fehlt.");
        var temporary = Path.Combine(directory, $".AndyTours-{Guid.NewGuid():N}.partial");
        var ownsTemporary = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                BufferSize = 81920,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var file = new FileStream(temporary, options))
            {
                ownsTemporary = true;
                await client.Download(selected, file, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                await file.FlushAsync(cancellationToken);
                if (file.Length != selected.Byteanzahl)
                    throw new InvalidDataException("Downloadgröße stimmt nicht mit dem Katalog überein.");
                file.Position = 0;
                var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken));
                if (!CryptographicOperations.FixedTimeEquals(
                        Convert.FromHexString(selected.InhaltHash), Convert.FromHexString(actualHash)))
                    throw new InvalidDataException("Downloadhash stimmt nicht mit dem Katalog überein.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!stillCurrent())
                throw new OperationCanceledException(cancellationToken);
            File.Move(temporary, target, overwrite: false);
            ownsTemporary = false;
        }
        finally
        {
            if (ownsTemporary)
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
