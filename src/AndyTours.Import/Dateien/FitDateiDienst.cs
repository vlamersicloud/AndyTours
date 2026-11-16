using System.Security.Cryptography;
using System.Text;
using AndyTours.Application.Stammdaten;
using AndyTours.Core.Modell;
using AndyTours.Import.Garmin;

namespace AndyTours.Import.Dateien;

/// <summary>Server-internal bounded FIT snapshot, parsing and immutable placement service.</summary>
public sealed class FitDateiDienst
{
    private const long MaxBytes = 128L * 1024 * 1024;
    private readonly string _root;
    private readonly TimeZoneInfo _zone;

    public FitDateiDienst(string datenordner, TimeZoneInfo zeitzone)
    {
        if (string.IsNullOrWhiteSpace(datenordner) || !Path.IsPathFullyQualified(datenordner))
            throw new ArgumentException("Serverdatenordner ungültig.", nameof(datenordner));
        _root = Path.GetFullPath(datenordner);
        _zone = zeitzone ?? throw new ArgumentNullException(nameof(zeitzone));
    }

    internal TimeZoneInfo FallbackZeitzone => _zone;

    internal async Task<FitDateiSnapshot> Lesen(string source, string tempDirectory, CancellationToken cancellationToken)
    {
        source = Resolve(source);
        tempDirectory = Resolve(tempDirectory);
        var temporary = Path.Combine(tempDirectory, ".fit-review-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(tempDirectory);
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await CopyBounded(input, output, cancellationToken).ConfigureAwait(false);
            await using var stream = new FileStream(temporary, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var fileHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fileHash + "\n" + _zone.ToSerializedString())));
            var parsed = LeseFitAufzeichnung.Ausfuehren(temporary, _zone).Aufzeichnung;
            if (parsed is null || !double.IsFinite(parsed.DistanzKm) || parsed.DistanzKm < 0 ||
                parsed.Fahrzeit < TimeSpan.Zero || parsed.Fahrzeit.Ticks % TimeSpan.TicksPerSecond != 0)
                throw new InvalidDataException();
            return new FitDateiSnapshot(parsed, fileHash, version, temporary);
        }
        catch (OperationCanceledException) { TryDelete(temporary); throw; }
        catch (StammdatenException)
        {
            TryDelete(temporary);
            throw;
        }
        catch (Exception)
        {
            TryDelete(temporary);
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Datei ungültig.");
        }
    }

    internal async Task<string> UnveraenderlichAblegen(string relativeTarget, FitDateiSnapshot fit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fit);
        var destination = Resolve(Path.Combine(_root, relativeTarget.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(_root, destination);
        if (relative == "." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Ablage ungültig.");
        var directory = Path.GetDirectoryName(destination)!;
        var temporary = Path.Combine(directory, ".fit-place-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(directory);
            if (File.Exists(destination))
            {
                await VerifyExisting(destination, fit.DateiHash, cancellationToken).ConfigureAwait(false);
                return relative.Replace(Path.DirectorySeparatorChar, '/');
            }
            await using (var input = new FileStream(fit.TempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await CopyBounded(input, output, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            try { File.Move(temporary, destination); }
            catch (IOException) when (File.Exists(destination))
            {
                await VerifyExisting(destination, fit.DateiHash, cancellationToken).ConfigureAwait(false);
            }
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Ablage ungültig.");
        }
        finally { TryDelete(temporary); }
    }

    private string Resolve(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var prefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(full, _root, comparison) && !full.StartsWith(prefix, comparison))
                throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Pfad ungültig.");
            return full;
        }
        catch (StammdatenException) { throw; }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Pfad ungültig.");
        }
    }

    private static async Task CopyBounded(Stream input, Stream output, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            total += read;
            if (total > MaxBytes) throw new InvalidDataException();
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
        if (total == 0) throw new InvalidDataException();
    }

    private static async Task VerifyExisting(string path, string expectedHash, CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxBytes) throw new InvalidDataException();
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

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

internal sealed class FitDateiSnapshot(Aufzeichnung aufzeichnung, string dateiHash, string version, string tempPath) : IDisposable
{
    public Aufzeichnung Aufzeichnung { get; } = aufzeichnung;
    public string DateiHash { get; } = dateiHash;
    public string Version { get; } = version;
    public string TempPath { get; } = tempPath;
    public void Dispose()
    {
        try { if (File.Exists(TempPath)) File.Delete(TempPath); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
