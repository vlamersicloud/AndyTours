using System.Globalization;
using System.Net;
using AndyTours.Contracts.V1;
using AndyTours.Application.Garmin;
using AndyTours.Import.Garmin;

namespace AndyTours.Import.Garmin;

/// <summary>Server-side ZIP/FIT artifact access. Paths are derived only from fixed server storage and IDs.</summary>
public sealed class GarminImportArtefakte
{
    private readonly string _datenordner;
    private readonly TimeZoneInfo _fallbackZeitzone;

    public GarminImportArtefakte(string datenordner, TimeZoneInfo? fallbackZeitzone = null)
    {
        if (string.IsNullOrWhiteSpace(datenordner) || !Path.IsPathFullyQualified(datenordner))
            throw new ArgumentException("Serverdatenordner ungültig.", nameof(datenordner));
        _datenordner = Path.GetFullPath(datenordner);
        _fallbackZeitzone = fallbackZeitzone ?? TimeZoneInfo.Local;
    }

    public async Task<GarminImportKandidatAntwort> Laden(
        GarminPersonalClient client, Guid jobId, GarminImportKandidatAntwort kandidat, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (jobId == Guid.Empty || kandidat is null || kandidat.KandidatId == Guid.Empty || kandidat.GarminAktivitaetId <= 0)
            throw new ArgumentException("Garmin-Kandidat ungültig.");

        ct.ThrowIfCancellationRequested();
        var archives = Path.Combine(_datenordner, "GarminConnect");
        var fitDirectory = Path.Combine(_datenordner, "GarminImport", jobId.ToString("N"));
        var id = kandidat.GarminAktivitaetId.ToString(CultureInfo.InvariantCulture);
        var archive = Path.Combine(archives, $"activity-{id}.zip");
        var fit = Path.Combine(fitDirectory, kandidat.KandidatId.ToString("N") + ".fit");

        try
        {
            Directory.CreateDirectory(archives);
            Directory.CreateDirectory(fitDirectory);
            if (!File.Exists(archive))
                await DownloadArchive(client, kandidat.GarminAktivitaetId, archive, ct).ConfigureAwait(false);

            var fitTemp = fit + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await LadeGarminConnectAufzeichnungen.EntpackeFitAsync(archive, fitTemp, ct).ConfigureAwait(false);
                var parsed = LeseFitAufzeichnung.Ausfuehren(fitTemp, _fallbackZeitzone, ct);
                var record = parsed.Aufzeichnung;
                if (record is null || !double.IsFinite(record.DistanzKm) || record.DistanzKm < 0 ||
                    record.Fahrzeit < TimeSpan.Zero || record.Fahrzeit.Ticks % TimeSpan.TicksPerSecond != 0)
                {
                    return kandidat with { Status = GarminImportKandidatStatus.Fehler, Warncode = GarminImportWarncodes.FitUngueltig };
                }

                ct.ThrowIfCancellationRequested();
                File.Move(fitTemp, fit, overwrite: true);
                return kandidat with
                {
                    Status = GarminImportKandidatStatus.FitBereit,
                    Datum = record.Datum,
                    Name = SafeName(record.Profil, kandidat.GarminAktivitaetId),
                    DistanzKm = record.DistanzKm,
                    FahrzeitSekunden = checked((long)record.Fahrzeit.TotalSeconds),
                    Warncode = null
                };
            }
            catch (InvalidDataException)
            {
                TryDelete(archive);
                return kandidat with { Status = GarminImportKandidatStatus.Fehler, Warncode = GarminImportWarncodes.ArchivUngueltig };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new GarminImportPaketFehler(GarminImportWarncodes.DateiFehler);
            }
            finally
            {
                TryDelete(fitTemp);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new GarminImportPaketFehler(GarminImportWarncodes.Zeitlimit);
        }
        catch (HttpRequestException ex)
        {
            throw new GarminImportPaketFehler(ex.StatusCode == HttpStatusCode.TooManyRequests
                ? GarminImportWarncodes.RateLimit : GarminImportWarncodes.Netzwerk);
        }
        catch (InvalidDataException)
        {
            throw new GarminImportPaketFehler(GarminImportWarncodes.ArchivUngueltig);
        }
        catch (IOException)
        {
            throw new GarminImportPaketFehler(GarminImportWarncodes.DateiFehler);
        }
        catch (UnauthorizedAccessException)
        {
            throw new GarminImportPaketFehler(GarminImportWarncodes.DateiFehler);
        }
    }

    private static async Task DownloadArchive(GarminPersonalClient client, long id, string archive, CancellationToken ct)
    {
        var temporary = archive + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await client.LadeArchivAsync(id, stream, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            try { File.Move(temporary, archive); }
            catch (IOException) when (File.Exists(archive)) { /* A competing worker completed the shared cache. */ }
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private static string SafeName(string? profile, long id)
    {
        var value = new string((profile ?? string.Empty).Where(c => !char.IsControl(c)).Take(120).ToArray()).Trim();
        return value.Length == 0 ? "Garmin " + id.ToString(CultureInfo.InvariantCulture) : value;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

/// <summary>Safe package-level interruption; contains only an allowlisted warning code, never the cause.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "The established German domain name is part of the import contract.")]
public sealed class GarminImportPaketFehler : Exception
{
    public GarminImportPaketFehler(string warncode)
        : base("Garmin-Importpaket konnte nicht verarbeitet werden.")
    {
        if (warncode is not (GarminImportWarncodes.Netzwerk or GarminImportWarncodes.Zeitlimit or
            GarminImportWarncodes.Abgebrochen or GarminImportWarncodes.ArchivUngueltig or GarminImportWarncodes.DateiFehler or GarminImportWarncodes.RateLimit or
            GarminImportWarncodes.SchluesselFehlt))
            throw new ArgumentException("Warncode ungültig.", nameof(warncode));
        Warncode = warncode;
    }

    public string Warncode { get; }
}
