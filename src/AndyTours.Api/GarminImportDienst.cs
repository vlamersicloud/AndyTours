using AndyTours.Import.Garmin;
using AndyTours.Application.Koordination;

namespace AndyTours.Api;

/// <summary>Sequential durable Garmin worker hosted by the API process.</summary>
public sealed class GarminImportDienst(GarminImportPaketWorker worker, IDatenbestandskoordination koordination, ILogger<GarminImportDienst> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> WorkerRetry = LoggerMessage.Define(LogLevel.Warning, new EventId(1, "GarminImportRetry"), "Garmin-Importworker konnte einen Durchlauf nicht abschließen; erneuter Versuch folgt.");
    private static readonly Guid WorkerId = Guid.NewGuid();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool handled;
                using (await koordination.Betreten(stoppingToken).ConfigureAwait(false))
                    handled = await worker.NaechstesPaket(WorkerId, stoppingToken).ConfigureAwait(false);
                if (!handled)
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                WorkerRetry(logger, null);
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
