using System.Security.Cryptography;
using AndyTours.Application.Stammdaten;
using AndyTours.Core.Modell;
using AndyTours.Import.Garmin;

namespace AndyTours.Import.Dateien;

public sealed partial class FitDateiDienst
{
    internal async Task<FitVerlaufSnapshot> VerlaufLesen(string relativerPfad, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = ResolveStoredRelativeFit(relativerPfad);
        var directory = Path.Combine(_root, ".fit-snapshots");
        var temporary = Path.Combine(directory, ".fit-verlauf-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(directory);
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await CopyBounded(input, output, cancellationToken).ConfigureAwait(false);

            await using var stream = new FileStream(temporary, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            cancellationToken.ThrowIfCancellationRequested();
            var (points, error) = LeseFitVerlauf.Ausfuehren(temporary);
            if (points is null || error is not null)
                throw Invalid();
            return new FitVerlaufSnapshot(points, hash, temporary);
        }
        catch (OperationCanceledException) { TryDelete(temporary); throw; }
        catch (StammdatenException) { TryDelete(temporary); throw; }
        catch (Exception)
        {
            TryDelete(temporary);
            throw Invalid();
        }
    }

    private string ResolveStoredRelativeFit(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.IndexOfAny([':', '\0']) >= 0)
            throw Invalid();
        var normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.StartsWith("//", StringComparison.Ordinal) ||
            Path.IsPathRooted(normalized) || !normalized.EndsWith(".fit", StringComparison.OrdinalIgnoreCase))
            throw Invalid();
        var segments = normalized.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw Invalid();
        var combined = Path.Combine(_root, Path.Combine(segments));
        return Resolve(combined);
    }

    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, "FIT-Verlauf ungültig.");
}

internal sealed class FitVerlaufSnapshot(
    IReadOnlyList<Verlaufspunkt> punkte,
    string dateiHash,
    string tempPath) : IDisposable
{
    public IReadOnlyList<Verlaufspunkt> Punkte { get; } = punkte;
    public string DateiHash { get; } = dateiHash;
    public string TempPath { get; } = tempPath;

    public void Dispose()
    {
        try { if (File.Exists(TempPath)) File.Delete(TempPath); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
