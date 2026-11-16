using System.Net;
using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.Import.Garmin;

/// <summary>Server-side orchestrator for one durable Garmin download packet; no HTTP routes or background hosting.</summary>
public sealed class GarminImportPaketWorker
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    private readonly IGarminImportArbeitspeicher _store;
    private readonly IGarminImportServerdaten _serverdaten;
    private readonly GarminImportArtefakte _artefakte;
    private readonly Func<HttpClient> _createHttpClient;

    public GarminImportPaketWorker(
        IGarminImportArbeitspeicher store,
        IGarminImportServerdaten serverdaten,
        GarminImportArtefakte artefakte,
        Func<HttpClient>? createHttpClient = null)
    {
        _store = store;
        _serverdaten = serverdaten;
        _artefakte = artefakte;
        _createHttpClient = createHttpClient ?? CreateHttpClient;
    }

    public async Task<bool> NaechstesPaket(Guid besitzer, CancellationToken ct = default)
    {
        var lease = await _store.Beanspruchen(besitzer, LeaseDuration, ct).ConfigureAwait(false);
        if (lease is null) return false;

        using var run = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var heartbeatStop = new CancellationTokenSource();
        using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(run.Token, heartbeatStop.Token);
        var heartbeatTask = Heartbeat(lease, run, heartbeat.Token);
        var warnings = new HashSet<string>(StringComparer.Ordinal);
        var interrupted = false;
        var more = true;
        HttpClient? http = null;
        try
        {
            var server = await _serverdaten.Laden(run.Token).ConfigureAwait(false);
            http = _createHttpClient();
            var client = new GarminPersonalClient(http, server.Schluessel);
            var observed = await _store.BeobachteteKandidaten(lease, run.Token).ConfigureAwait(false);
            var knownIds = observed.Select(item => item.Kandidat.GarminAktivitaetId).ToHashSet();
            knownIds.UnionWith(server.BereitsImportiert);
            var currentPacket = observed.Where(item => item.Paketnummer == lease.Auftrag.Paketnummer).ToList();
            var current = currentPacket.Select(item => item.Kandidat)
                .Where(item => item.Status is GarminImportKandidatStatus.Wartend or GarminImportKandidatStatus.ZipBereit or GarminImportKandidatStatus.Fehler)
                .OrderBy(item => item.GarminAktivitaetId).ToList();

            foreach (var candidate in current)
            {
                run.Token.ThrowIfCancellationRequested();
                var result = await LoadArtifact(client, lease.JobId, candidate, run.Token).ConfigureAwait(false);
                await _store.DownloadKandidatSpeichern(lease, result, run.Token).ConfigureAwait(false);
                if (result.Warncode is not null) warnings.Add(result.Warncode);
            }

            var pendingCount = currentPacket.Count;
            if (pendingCount < 20)
            {
                await using var ids = client.LeseRadaktivitaetsIdsAsync(run.Token).GetAsyncEnumerator(run.Token);
                while (pendingCount < 20)
                {
                    if (!await ids.MoveNextAsync().ConfigureAwait(false))
                    {
                        more = false;
                        break;
                    }

                    var id = ids.Current;
                    if (knownIds.Contains(id)) continue;
                    run.Token.ThrowIfCancellationRequested();
                    await Task.Delay(TimeSpan.FromMilliseconds(500), run.Token).ConfigureAwait(false);
                    var candidate = new GarminImportKandidatAntwort(Guid.NewGuid(), id, GarminImportKandidatStatus.Wartend,
                        null, "Garmin " + id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, null, null);
                    candidate = await _store.DownloadKandidatSpeichern(lease, candidate, run.Token).ConfigureAwait(false);
                    knownIds.Add(id);
                    pendingCount++;
                    var result = await LoadArtifact(client, lease.JobId, candidate, run.Token).ConfigureAwait(false);
                    result = await _store.DownloadKandidatSpeichern(lease, result, run.Token).ConfigureAwait(false);
                    if (result.Warncode is not null) warnings.Add(result.Warncode);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            interrupted = true;
            warnings.Add(GarminImportWarncodes.Abgebrochen);
        }
        catch (GarminImportPaketFehler error)
        {
            interrupted = true;
            warnings.Add(error.Warncode);
        }
        catch (HttpRequestException error)
        {
            interrupted = true;
            warnings.Add(error.StatusCode == HttpStatusCode.TooManyRequests ? GarminImportWarncodes.RateLimit : GarminImportWarncodes.Netzwerk);
        }
        catch (OperationCanceledException)
        {
            interrupted = true;
            warnings.Add(GarminImportWarncodes.Zeitlimit);
        }
        catch (IOException)
        {
            interrupted = true;
            warnings.Add(GarminImportWarncodes.DateiFehler);
        }
        catch (StammdatenException)
        {
            interrupted = true;
            warnings.Add(GarminImportWarncodes.DateiFehler);
        }
        catch
        {
            interrupted = true;
            warnings.Add(GarminImportWarncodes.DateiFehler);
        }
        finally
        {
            heartbeatStop.Cancel();
            try { await heartbeatTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            http?.Dispose();
        }

        using var cleanup = ct.IsCancellationRequested ? new CancellationTokenSource(TimeSpan.FromSeconds(5)) : null;
        var completionToken = cleanup?.Token ?? CancellationToken.None;
        try
        {
            await _store.PaketAbschliessen(lease, interrupted, more, warnings.ToArray(), completionToken).ConfigureAwait(false);
        }
        catch (StammdatenException)
        {
            // A fenced or canceled stale lease cannot finalize the job.
        }
        return true;
    }

    private async Task Heartbeat(GarminImportLease lease, CancellationTokenSource run, CancellationToken stop)
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stop).ConfigureAwait(false);
            try
            {
                if (await _store.Verlaengern(lease, LeaseDuration, stop).ConfigureAwait(false) is null)
                {
                    run.Cancel();
                    return;
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { return; }
            catch
            {
                run.Cancel();
                return;
            }
        }
    }

    private async Task<GarminImportKandidatAntwort> LoadArtifact(GarminPersonalClient client, Guid jobId, GarminImportKandidatAntwort candidate, CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
        return await _artefakte.Laden(client, jobId, candidate, ct).ConfigureAwait(false);
    }

    private static HttpClient CreateHttpClient() => new(new SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromMinutes(2)
    };
}
