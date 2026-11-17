namespace AndyTours.Daten.Datensicherung;

using System.Security;
using System.Security.Cryptography;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

/// <summary>Server-side create-or-return coordinator. The API must hold the outer exclusive dataset lease. External writers and filesystem TOCTOU races are not runtime-verified and must be excluded by deployment; this does not acquire a nested lease or authorize restore.</summary>
public sealed class DateisystemSicherungsarchivErstellung : ISicherungsarchivErstellung
{
    private const string Fehlertext = "Sicherungsarchiv konnte nicht erstellt werden.";
    private readonly string _datenordner;
    private readonly string _backupOrdner;
    private readonly SicherungsarchivGrenzen _limits;
    private readonly TimeProvider _timeProvider;

    public DateisystemSicherungsarchivErstellung(string datenordner, string backupOrdner,
        SicherungsarchivGrenzen limits, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datenordner);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupOrdner);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (!Path.IsPathFullyQualified(datenordner) || !Path.IsPathFullyQualified(backupOrdner)) throw Invalid();
        _datenordner = Path.TrimEndingDirectorySeparator(Path.GetFullPath(datenordner));
        _backupOrdner = Path.TrimEndingDirectorySeparator(Path.GetFullPath(backupOrdner));
        _limits = limits;
        _timeProvider = timeProvider;
        ValidateConfiguration();
    }

    public async Task<SicherungsarchivErstellungsantwort> Erstellen(
        SicherungsarchivErstellungsanfrage anfrage, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (anfrage is null || anfrage.VorgangsId == Guid.Empty) throw Invalid();
            ValidateConfiguration();
            var finalPath = Sicherungsarbeitsbereich.Zielpfad(_backupOrdner, anfrage.VorgangsId);
            if (ExistsAny(finalPath))
                return new SicherungsarchivErstellungsantwort(await InspectExistingAsync(finalPath, anfrage.VorgangsId, cancellationToken), true);

            using var workspace = Sicherungsarbeitsbereich.Oeffnen(_backupOrdner, anfrage.VorgangsId, cancellationToken);
            using var snapshot = SicherungsdatenbankSnapshot.Erstellen(
                Path.Combine(_datenordner, "andytours.db"), workspace.DatenbankOrdner, _limits.Manifest, cancellationToken);
            var dbBytes = new FileInfo(snapshot.SnapshotPfad).Length;
            var exclusions = GetExclusions();
            var files = SicherungsquellenInventar.Ermittle(_datenordner, _backupOrdner,
                snapshot.ReferenzierteRelativePfade, dbBytes, _limits.Manifest, cancellationToken);
            var tempPath = workspace.ArchivZwischendatei;
            byte[] digest;
            long bytes;
            Sicherungsmanifest manifest;
            await using (var output = CreateNewPrivateFile(tempPath))
            {
                var createdUtc = _timeProvider.GetUtcNow().ToUniversalTime();
                manifest = await SicherungsarchivPaketierung.Erstellen(output, _datenordner, snapshot,
                    files, createdUtc, _limits.Manifest, exclusions, _limits.MaxArchivBytes,
                    _limits.MaxManifestBytes, cancellationToken);
                await output.FlushAsync(cancellationToken);
                bytes = output.Length;
                if (bytes <= 0 || bytes > _limits.MaxArchivBytes) throw Invalid();
                output.Position = 0;
                _ = await SicherungsarchivPruefung.Pruefe(output, _limits.Manifest, exclusions,
                    _limits.MaxArchivBytes, _limits.MaxManifestBytes, _limits.MaxZentralverzeichnisBytes, cancellationToken);
                output.Position = 0;
                digest = await SHA256.HashDataAsync(output, cancellationToken);
            }

            snapshot.PruefeUnveraendert(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, finalPath, overwrite: false);
            var response = new SicherungAntwort(anfrage.VorgangsId, Sicherungsart.Vollarchiv,
                manifest.ErstelltUtc, bytes, Convert.ToHexString(digest));
            return new SicherungsarchivErstellungsantwort(response, false);
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException ex) when (ex.Art == StammdatenFehlerart.Konflikt) { throw; }
        catch (StammdatenException) { throw Invalid(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                                        OverflowException or CryptographicException or SecurityException or
                                        NotSupportedException or ObjectDisposedException)
        { throw Invalid(); }
    }

    private async Task<SicherungAntwort> InspectExistingAsync(string path, Guid id, CancellationToken ct)
    {
        try
        {
            if (HasReparseAncestor(path)) throw Invalid();
            var attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) throw Invalid();
            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var info = new FileInfo(path);
            var handleLength = input.Length;
            if (handleLength <= 0 || handleLength > _limits.MaxArchivBytes || info.Length != handleLength) throw Invalid();
            var exclusions = GetExclusions();
            var manifest = await SicherungsarchivPruefung.Pruefe(input, _limits.Manifest, exclusions,
                _limits.MaxArchivBytes, _limits.MaxManifestBytes, _limits.MaxZentralverzeichnisBytes, ct);
            var after = new FileInfo(path);
            if (!after.Exists || after.Length != handleLength || after.LastWriteTimeUtc != info.LastWriteTimeUtc || HasReparseAncestor(path))
                throw Invalid();
            input.Position = 0;
            var hash = await SHA256.HashDataAsync(input, ct);
            var postHash = new FileInfo(path);
            if (!postHash.Exists || input.Length != handleLength || postHash.Length != handleLength ||
                postHash.LastWriteTimeUtc != info.LastWriteTimeUtc || HasReparseAncestor(path)) throw Invalid();
            return new SicherungAntwort(id, Sicherungsart.Vollarchiv, manifest.ErstelltUtc,
                handleLength, Convert.ToHexString(hash));
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw Invalid(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                                        OverflowException or CryptographicException or SecurityException or
                                        NotSupportedException or ObjectDisposedException)
        { throw Invalid(); }
    }

    private System.Collections.ObjectModel.ReadOnlyCollection<string> GetExclusions()
    {
        var exclusions = new List<string> { "Sicherungen" };
        if (IsWithin(_datenordner, _backupOrdner))
            exclusions.Add(Path.GetRelativePath(_datenordner, _backupOrdner).Replace(Path.DirectorySeparatorChar, '/'));
        return exclusions.AsReadOnly();
    }

    private void ValidateConfiguration()
    {
        if (SamePath(_datenordner, _backupOrdner) || !Directory.Exists(_datenordner) || HasReparseAncestor(_datenordner) ||
            HasReparseAncestor(_backupOrdner) || _limits.Manifest is null || _limits.Manifest.MaxEintraege <= 0 ||
            _limits.Manifest.MaxDateiBytes <= 0 || _limits.Manifest.MaxGesamtBytes <= 0 || _limits.Manifest.MaxPfadzeichen <= 0 ||
            _limits.MaxArchivBytes <= 0 || _limits.MaxManifestBytes <= 0 || _limits.MaxManifestBytes > Array.MaxLength ||
            _limits.MaxZentralverzeichnisBytes <= 0)
            throw Invalid();
    }

    private static FileStream CreateNewPrivateFile(string path)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.None, BufferSize = 81920 };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(path, options);
    }

    private static bool ExistsAny(string path)
    {
        try { _ = File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    private static bool HasReparseAncestor(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true; }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || SamePath(parent, current)) break;
            current = parent;
        }
        return false;
    }

    private static bool SamePath(string a, string b) => string.Equals(a, b,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool IsWithin(string parent, string candidate)
    {
        var relative = Path.GetRelativePath(parent, candidate);
        return relative != "." && relative != ".." && !Path.IsPathRooted(relative) &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, Fehlertext);
}
