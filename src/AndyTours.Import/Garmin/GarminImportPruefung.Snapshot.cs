using System.Security.Cryptography;
using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Import.Garmin;

namespace AndyTours.Import.Garmin;

public sealed partial class GarminImportPruefung
{
    private readonly IGarminImportPruefspeicher _speicher;
    private readonly string _datenordner;

    public GarminImportPruefung(IGarminImportPruefspeicher speicher, string datenordner)
    {
        _speicher = speicher ?? throw new ArgumentNullException(nameof(speicher));
        if (string.IsNullOrWhiteSpace(datenordner) || !Path.IsPathFullyQualified(datenordner))
            throw new ArgumentException("Serverdatenordner ungültig.", nameof(datenordner));
        _datenordner = Path.GetFullPath(datenordner);
    }

    private async Task<FitSnapshot> LeseSnapshot(Guid jobId, Guid kandidatId, CancellationToken cancellationToken)
    {
        if (jobId == Guid.Empty || kandidatId == Guid.Empty)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Datei ungültig.");
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.Combine(_datenordner, "GarminImport", jobId.ToString("N"));
        var source = Path.Combine(directory, kandidatId.ToString("N") + ".fit");
        var temporary = Path.Combine(directory, ".review-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            Directory.CreateDirectory(directory);
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await GarminPersonalClient.KopiereBegrenztAsync(input, output, 128L * 1024 * 1024, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await using var snapshotStream = new FileStream(temporary, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(snapshotStream, cancellationToken).ConfigureAwait(false));
            var parsed = LeseFitAufzeichnung.Ausfuehren(temporary);
            var record = parsed.Aufzeichnung;
            if (record is null || !double.IsFinite(record.DistanzKm) || record.DistanzKm < 0 || record.Fahrzeit < TimeSpan.Zero || record.Fahrzeit.Ticks % TimeSpan.TicksPerSecond != 0)
                throw new InvalidDataException();
            return new FitSnapshot(record, hash, temporary);
        }
        catch (OperationCanceledException)
        {
            TryDelete(temporary);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or FormatException or OverflowException)
        {
            TryDelete(temporary);
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Datei ungültig.");
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed class FitSnapshot(Aufzeichnung aufzeichnung, string version, string tempPath) : IDisposable
    {
        public Aufzeichnung Aufzeichnung { get; } = aufzeichnung;
        public string Version { get; } = version;
        public string TempPath { get; } = tempPath;
        public void Dispose() => TryDelete(TempPath);
    }
}
