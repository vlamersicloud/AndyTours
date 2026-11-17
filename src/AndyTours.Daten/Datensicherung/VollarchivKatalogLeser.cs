namespace AndyTours.Daten.Datensicherung;

using System.Security;
using System.Security.Cryptography;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

/// <summary>
/// Read-only verifier for creator-named full archives. It does not extract files or prove restore validity.
/// The caller must deploy the backup folder without external writers; filesystem TOCTOU races are not excluded.
/// </summary>
public static class VollarchivKatalogLeser
{
    private const string Praefix = "andytours_full_";
    private const string Endung = ".zip";
    private static readonly string[] KeineWeiterenAusschluesse = [];

    /// <summary>
    /// Lists fully verified full archives directly in an existing or missing backup folder.
    /// Limits are required only when at least one exact creator-named archive is found.
    /// </summary>
    public static async Task<IReadOnlyList<SicherungAntwort>> Liste(
        string fixedAbsoluteOrdner,
        VollarchivKatalogGrenzen? limits,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(fixedAbsoluteOrdner) ||
                !Path.IsPathFullyQualified(fixedAbsoluteOrdner))
                throw Unavailable();

            var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(fixedAbsoluteOrdner));
            if (HasReparseAncestor(folder)) throw Unavailable();
            FileAttributes folderAttributes;
            try { folderAttributes = File.GetAttributes(folder); }
            catch (DirectoryNotFoundException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Array.Empty<SicherungAntwort>();
            }
            catch (FileNotFoundException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Array.Empty<SicherungAntwort>();
            }
            if ((folderAttributes & FileAttributes.Directory) == 0 || HasReparseAncestor(folder))
                throw Unavailable();

            var results = new List<SicherungAntwort>();
            var ids = new HashSet<Guid>();
            var seen = 0;
            long remainingPayloadBytes = 0;
            VollarchivKatalogGrenzen? configuredLimits = null;
            var limitsValidated = false;

            foreach (var path in Directory.EnumerateFileSystemEntries(folder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryParseCreatorName(Path.GetFileName(path), out var id)) continue;

                if (!limitsValidated)
                {
                    ValidateLimits(limits);
                    configuredLimits = limits!;
                    remainingPayloadBytes = configuredLimits.MaxNutzdatenBytes;
                    limitsValidated = true;
                }

                var bounds = configuredLimits!;
                if (seen >= bounds.MaxVollarchive) throw Unavailable();
                seen++;
                if (!ids.Add(id)) throw Unavailable();

                var fullPath = Path.GetFullPath(path);
                if (!SamePath(Path.GetDirectoryName(fullPath)!, folder) || HasReparseAncestor(fullPath))
                    throw Unavailable();
                var attributes = File.GetAttributes(fullPath);
                if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                    throw Unavailable();

                var before = new FileInfo(fullPath);
                if (!before.Exists) throw Unavailable();
                var beforeWrite = before.LastWriteTimeUtc;
                await using var input = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var length = input.Length;
                if (length <= 0 || length > bounds.Archiv.MaxArchivBytes || before.Length != length)
                    throw Unavailable();

                var remainingManifest = bounds.Archiv.Manifest with
                {
                    MaxGesamtBytes = Math.Min(bounds.Archiv.Manifest.MaxGesamtBytes, remainingPayloadBytes)
                };
                if (remainingManifest.MaxGesamtBytes <= 0) throw Unavailable();

                Sicherungsmanifest manifest;
                try
                {
                    manifest = await SicherungsarchivPruefung.Pruefe(
                        input, remainingManifest, KeineWeiterenAusschluesse,
                        bounds.Archiv.MaxArchivBytes, bounds.Archiv.MaxManifestBytes,
                        bounds.Archiv.MaxZentralverzeichnisBytes, cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) when (ex is StammdatenException or IOException or UnauthorizedAccessException or
                                                ArgumentException or NotSupportedException or OverflowException or
                                                CryptographicException or SecurityException or ObjectDisposedException)
                {
                    throw Unavailable();
                }

                long archivePayloadBytes = 0;
                foreach (var entry in manifest.Eintraege)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    archivePayloadBytes = checked(archivePayloadBytes + entry.Byteanzahl);
                }
                if (archivePayloadBytes > remainingPayloadBytes) throw Unavailable();

                input.Position = 0;
                var digest = await SHA256.HashDataAsync(input, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                var after = new FileInfo(fullPath);
                after.Refresh();
                if (!after.Exists || input.Length != length || after.Length != length ||
                    after.LastWriteTimeUtc != beforeWrite || HasReparseAncestor(fullPath))
                    throw Unavailable();

                remainingPayloadBytes -= archivePayloadBytes;
                results.Add(new SicherungAntwort(id, Sicherungsart.Vollarchiv,
                    manifest.ErstelltUtc, length, Convert.ToHexString(digest)));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return results.AsReadOnly();
        }
        catch (OperationCanceledException) { throw; }
        catch (SicherungskatalogNichtVerfuegbarException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                                        NotSupportedException or OverflowException or CryptographicException or
                                        SecurityException or ObjectDisposedException)
        {
            throw Unavailable();
        }
    }

    private static bool TryParseCreatorName(string name, out Guid id)
    {
        id = Guid.Empty;
        if (!name.StartsWith(Praefix, StringComparison.Ordinal) ||
            !name.EndsWith(Endung, StringComparison.Ordinal) ||
            name.Length != Praefix.Length + 32 + Endung.Length)
            return false;

        var value = name.Substring(Praefix.Length, 32);
        return Guid.TryParseExact(value, "N", out id) &&
               id != Guid.Empty &&
               string.Equals(id.ToString("N"), value, StringComparison.Ordinal);
    }

    private static void ValidateLimits(VollarchivKatalogGrenzen? limits)
    {
        var archive = limits?.Archiv;
        var manifest = archive?.Manifest;
        if (limits is null || archive is null || manifest is null ||
            limits.MaxVollarchive <= 0 || limits.MaxNutzdatenBytes <= 0 ||
            manifest.MaxEintraege <= 0 || manifest.MaxEintraege >= int.MaxValue ||
            manifest.MaxDateiBytes <= 0 || manifest.MaxGesamtBytes <= 0 || manifest.MaxPfadzeichen <= 0 ||
            manifest.MaxDateiBytes > manifest.MaxGesamtBytes ||
            archive.MaxArchivBytes <= 0 || archive.MaxManifestBytes <= 0 ||
            archive.MaxManifestBytes > Array.MaxLength || archive.MaxManifestBytes > archive.MaxArchivBytes ||
            archive.MaxZentralverzeichnisBytes <= 0 || archive.MaxZentralverzeichnisBytes > archive.MaxArchivBytes)
            throw Unavailable();
    }

    private static bool HasReparseAncestor(string path)
    {
        for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current);)
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || SamePath(parent, current)) break;
            current = parent;
        }
        return false;
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(left, right,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static SicherungskatalogNichtVerfuegbarException Unavailable() => new();
}
