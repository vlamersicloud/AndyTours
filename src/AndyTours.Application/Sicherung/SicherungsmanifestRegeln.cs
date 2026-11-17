namespace AndyTours.Application.Sicherung;

using System.Collections.ObjectModel;
using System.Text;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

public static class SicherungsmanifestRegeln
{
    private const string Fehlertext = "Sicherungsmanifest ungültig.";
    private static readonly StringComparer PfadComparer = StringComparer.OrdinalIgnoreCase;

    public static Sicherungsmanifest Pruefe(
        Sicherungsmanifest manifest,
        SicherungsmanifestGrenzen grenzen,
        IReadOnlyList<string> ausgeschlosseneRelativePfade,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (manifest is null || grenzen is null || ausgeschlosseneRelativePfade is null ||
                grenzen.MaxEintraege <= 0 || grenzen.MaxDateiBytes <= 0 ||
                grenzen.MaxGesamtBytes <= 0 || grenzen.MaxPfadzeichen <= 0 ||
                manifest.Formatversion != 1 || manifest.ErstelltUtc.Offset != TimeSpan.Zero ||
                manifest.Eintraege is null || manifest.Eintraege.Count == 0 ||
                manifest.Eintraege.Count > grenzen.MaxEintraege)
                throw Invalid();

            var excluded = new List<string> { "Sicherungen" };
            foreach (var raw in ausgeschlosseneRelativePfade)
            {
                cancellationToken.ThrowIfCancellationRequested();
                excluded.Add(NormalizePath(raw, grenzen.MaxPfadzeichen));
            }

            var entries = new List<SicherungsmanifestEintrag>(manifest.Eintraege.Count);
            var paths = new HashSet<string>(PfadComparer);
            long total = 0;
            var databaseCount = 0;

            foreach (var entry in manifest.Eintraege)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry is null || !Enum.IsDefined(entry.Art) || entry.Byteanzahl < 0 ||
                    entry.Byteanzahl > grenzen.MaxDateiBytes ||
                    entry.InhaltHash is not { Length: 64 } || !entry.InhaltHash.All(Uri.IsHexDigit))
                    throw Invalid();

                var path = NormalizePath(entry.RelativerPfad, grenzen.MaxPfadzeichen);
                if (!string.Equals(path, entry.RelativerPfad, StringComparison.Ordinal) ||
                    !paths.Add(path) || IsReserved(path) || excluded.Any(prefix => HasPrefix(path, prefix)))
                    throw Invalid();

                if (entry.Art == SicherungsmanifestEintragart.Datenbank)
                {
                    databaseCount++;
                    if (!string.Equals(path, "andytours.db", StringComparison.Ordinal) || entry.Byteanzahl == 0)
                        throw Invalid();
                }
                else if (string.Equals(path, "andytours.db", StringComparison.OrdinalIgnoreCase))
                    throw Invalid();

                total = checked(total + entry.Byteanzahl);
                if (total > grenzen.MaxGesamtBytes) throw Invalid();
                entries.Add(entry);
            }

            if (databaseCount != 1) throw Invalid();
            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var separator = path.IndexOf('/');
                while (separator >= 0)
                {
                    if (paths.Contains(path[..separator])) throw Invalid();
                    separator = path.IndexOf('/', separator + 1);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return manifest with { Eintraege = new ReadOnlyCollection<SicherungsmanifestEintrag>(entries) };
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw; }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or EncoderFallbackException)
        {
            throw Invalid();
        }
    }

    private static string NormalizePath(string? path, int maxLength)
    {
        if (string.IsNullOrEmpty(path) || path.Length > maxLength || path[0] == '/' ||
            path.Contains('\\') || path.Contains(':') || path.Any(char.IsControl) ||
            !string.Equals(path, path.Normalize(NormalizationForm.FormC), StringComparison.Ordinal))
            throw Invalid();

        var segments = path.Split('/');
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment is "." or ".." ||
                segment.EndsWith(' ') || segment.EndsWith('.') ||
                segment.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0 ||
                IsDeviceName(segment))
                throw Invalid();
        }
        return string.Join('/', segments);
    }

    private static bool IsDeviceName(string segment)
    {
        var baseName = segment.Split('.')[0].TrimEnd(' ', '.');
        if (baseName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("NUL", StringComparison.OrdinalIgnoreCase))
            return true;
        if (baseName.Length == 4 && (baseName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                                     baseName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)))
            return baseName[3] is >= '1' and <= '9' or '¹' or '²' or '³';
        return false;
    }

    private static bool IsReserved(string path)
    {
        if (path.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("andytours.db-wal", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("andytours.db-shm", StringComparison.OrdinalIgnoreCase))
            return true;
        return path.Split('/').Any(segment =>
            segment.StartsWith(".upload-", StringComparison.OrdinalIgnoreCase) ||
            segment.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasPrefix(string path, string prefix) =>
        path.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);

    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, Fehlertext);
}
