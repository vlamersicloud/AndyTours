namespace AndyTours.Daten.Datensicherung;

using System.Collections.ObjectModel;
using System.Security;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

public sealed record SicherungsQuelldatei(string RelativerPfad, string VollstaendigerPfad, long Byteanzahl, DateTime LetzteAenderungUtc);

/// <summary>Read-only filesystem inventory. Requires the caller's exclusive in-process dataset lease.</summary>
public static class SicherungsquellenInventar
{
    private const string Fehlertext = "Sicherungsquellen ungültig.";

    public static IReadOnlyList<SicherungsQuelldatei> Ermittle(
        string datenordner,
        string sicherungsordner,
        IReadOnlyList<string> referenzierteRelativePfade,
        long datenbankByteanzahl,
        SicherungsmanifestGrenzen grenzen,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(datenordner) || string.IsNullOrWhiteSpace(sicherungsordner) ||
                !Path.IsPathFullyQualified(datenordner) || !Path.IsPathFullyQualified(sicherungsordner) ||
                referenzierteRelativePfade is null || grenzen is null || datenbankByteanzahl <= 0 ||
                grenzen.MaxEintraege <= 0 || grenzen.MaxDateiBytes <= 0 || grenzen.MaxGesamtBytes <= 0 || grenzen.MaxPfadzeichen <= 0 ||
                datenbankByteanzahl > grenzen.MaxDateiBytes || datenbankByteanzahl > grenzen.MaxGesamtBytes)
                throw Invalid();

            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(datenordner));
            var backup = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sicherungsordner));
            if (SamePath(root, backup) || !Directory.Exists(root) || HasReparseAncestor(root)) throw Invalid();
            var backupInside = IsWithin(root, backup);
            if (backupInside && HasReparseAncestor(backup)) throw Invalid();
            var excluded = new List<string> { "Sicherungen" };
            if (backupInside) excluded.Add(Path.GetRelativePath(root, backup).Replace(Path.DirectorySeparatorChar, '/'));

            var rootInfo = new DirectoryInfo(root);
            var result = new List<SicherungsQuelldatei>();
            long totalBytes = datenbankByteanzahl;
            var count = 1;
            Walk(rootInfo, root, backupInside ? backup : null, result, grenzen, ref count, ref totalBytes, cancellationToken);
            if (totalBytes > grenzen.MaxGesamtBytes || count > grenzen.MaxEintraege) throw Invalid();

            var known = new HashSet<string>(result.Select(x => x.RelativerPfad), StringComparer.Ordinal);
            foreach (var referenced in referenzierteRelativePfade)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (referenced is null || !known.Contains(referenced)) throw Invalid();
            }

            var manifestEntries = new List<SicherungsmanifestEintrag>(result.Count + 1)
            {
                new("andytours.db", SicherungsmanifestEintragart.Datenbank, datenbankByteanzahl, new string('0', 64))
            };
            manifestEntries.AddRange(result.Select(file => new SicherungsmanifestEintrag(
                file.RelativerPfad, SicherungsmanifestEintragart.Datei, file.Byteanzahl, new string('0', 64))));
            _ = SicherungsmanifestRegeln.Pruefe(
                new Sicherungsmanifest(1, DateTimeOffset.UnixEpoch, manifestEntries), grenzen, excluded, cancellationToken);

            result.Sort((left, right) => StringComparer.Ordinal.Compare(left.RelativerPfad, right.RelativerPfad));
            return new ReadOnlyCollection<SicherungsQuelldatei>(result);
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw Invalid(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                                        OverflowException or NotSupportedException or SecurityException)
        { throw Invalid(); }
    }

    private static void Walk(DirectoryInfo directory, string root, string? backupInside,
        List<SicherungsQuelldatei> files, SicherungsmanifestGrenzen limits, ref int count, ref long total,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var full = Path.GetFullPath(entry.FullName);
            if (backupInside is not null && (SamePath(full, backupInside) || IsWithin(backupInside, full))) continue;
            var relative = Path.GetRelativePath(root, full).Replace(Path.DirectorySeparatorChar, '/');
            if (relative.Length > limits.MaxPfadzeichen) throw Invalid();
            var attributes = entry.Attributes;
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw Invalid();
            if (entry is DirectoryInfo childDirectory)
            {
                if ((directory.FullName == root && string.Equals(childDirectory.Name, "Sicherungen", StringComparison.OrdinalIgnoreCase)) ||
                    relative.Split('/').Any(segment => segment.StartsWith(".upload-", StringComparison.OrdinalIgnoreCase)) ||
                    relative.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                Walk(childDirectory, root, backupInside, files, limits, ref count, ref total, cancellationToken);
                continue;
            }
            if (entry is not FileInfo file) throw Invalid();
            if (SamePath(full, Path.Combine(root, "andytours.db")) ||
                SamePath(full, Path.Combine(root, "andytours.db-wal")) ||
                SamePath(full, Path.Combine(root, "andytours.db-shm")) ||
                SamePath(full, Path.Combine(root, "andytours.db-journal"))) continue;
            if (relative.Split('/').Any(segment => segment.StartsWith(".upload-", StringComparison.OrdinalIgnoreCase)) ||
                relative.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
            var size = file.Length;
            if (size < 0 || size > limits.MaxDateiBytes) throw Invalid();
            count = checked(count + 1);
            total = checked(total + size);
            if (count > limits.MaxEintraege || total > limits.MaxGesamtBytes) throw Invalid();
            files.Add(new SicherungsQuelldatei(relative, full, size, file.LastWriteTimeUtc));
        }
    }

    private static bool HasReparseAncestor(string path)
    {
        var current = new DirectoryInfo(path);
        while (current is not null)
        {
            try { if ((File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0) return true; }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            current = current.Parent;
        }
        return false;
    }

    private static bool SamePath(string left, string right) => string.Equals(left, right,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool IsWithin(string parent, string candidate)
    {
        var relative = Path.GetRelativePath(parent, candidate);
        return relative != "." && relative != ".." && !Path.IsPathRooted(relative) &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, Fehlertext);
}
