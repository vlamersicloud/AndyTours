namespace AndyTours.Daten;

using System.Collections.ObjectModel;
using System.Security.Cryptography;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten.Datensicherung;

/// <summary>Read-only metadata reader for strict legacy SQLite backup files.</summary>
public sealed class DateisystemSicherungskatalog : ISicherungskatalog, IDisposable
{
    private readonly string _ordner;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, (string Hash, Guid Id)> _ids = new(StringComparer.Ordinal);
    private bool _disposed;

    public DateisystemSicherungskatalog(string sicherungsordner)
    {
        if (string.IsNullOrWhiteSpace(sicherungsordner) || !Path.IsPathFullyQualified(sicherungsordner))
            throw new ArgumentException("Sicherungsordner ungültig.", nameof(sicherungsordner));
        _ordner = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sicherungsordner));
    }

    public async Task<SicherungListeAntwort> Liste(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            FileAttributes directoryAttributes;
            try { directoryAttributes = File.GetAttributes(_ordner); }
            catch (DirectoryNotFoundException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ids.Clear();
                return new SicherungListeAntwort(Array.Empty<SicherungAntwort>());
            }
            catch (FileNotFoundException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ids.Clear();
                return new SicherungListeAntwort(Array.Empty<SicherungAntwort>());
            }
            if ((directoryAttributes & FileAttributes.Directory) == 0)
                throw Invalid("Sicherungskatalog konnte nicht gelesen werden.");
            RejectReparsePoint(_ordner);
            var listed = ErmittleSicherungen.Ausfuehren(_ordner);
            var entries = new List<SicherungAntwort>(listed.Count);
            var refreshed = new Dictionary<string, (string Hash, Guid Id)>(StringComparer.Ordinal);
            foreach (var item in listed)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = Path.GetFullPath(item.Datei);
                if (!string.Equals(Path.GetDirectoryName(path), _ordner, StringComparison.Ordinal))
                    throw Invalid("Sicherungskatalog konnte nicht gelesen werden.");
                RejectReparsePoint(path);
                var before = new FileInfo(path);
                if (!before.Exists) throw Changed();
                var length = before.Length;
                var write = before.LastWriteTimeUtc;
                string hash;
                await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                    81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    using var sha = SHA256.Create();
                    var digest = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
                    hash = Convert.ToHexString(digest);
                }
                cancellationToken.ThrowIfCancellationRequested();
                RejectReparsePoint(path);
                var after = new FileInfo(path);
                after.Refresh();
                if (!after.Exists || after.Length != length || after.LastWriteTimeUtc != write) throw Changed();
                var key = path;
                var id = _ids.TryGetValue(key, out var prior) && string.Equals(prior.Hash, hash, StringComparison.Ordinal)
                    ? prior.Id : Guid.NewGuid();
                refreshed[key] = (hash, id);
                var timestamp = Path.GetFileNameWithoutExtension(path)[Sicherungsdatei.Praefix.Length..];
                entries.Add(new SicherungAntwort(id, Sicherungsart.LegacySqlite, null, length, hash)
                {
                    LegacyZeitstempel = timestamp
                });
            }
            cancellationToken.ThrowIfCancellationRequested();
            _ids.Clear();
            foreach (var pair in refreshed) _ids.Add(pair.Key, pair.Value);
            return new SicherungListeAntwort(new ReadOnlyCollection<SicherungAntwort>(entries));
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw Invalid("Sicherungskatalog konnte nicht gelesen werden.");
        }
        finally { _gate.Release(); }
    }

    private static void RejectReparsePoint(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            var attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw Invalid("Sicherungskatalog konnte nicht gelesen werden.");
            if (Path.GetDirectoryName(current) == current) break;
        }
    }

    private static StammdatenException Invalid(string message) =>
        new(StammdatenFehlerart.Ungueltig, message);

    private static StammdatenException Changed() =>
        new(StammdatenFehlerart.Konflikt, "Sicherung wurde während des Lesens geändert.");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gate.Dispose();
        _ids.Clear();
    }
}
