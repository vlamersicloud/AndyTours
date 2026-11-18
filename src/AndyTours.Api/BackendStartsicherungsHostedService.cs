namespace AndyTours.Api;

using AndyTours.Application.Sicherung;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Runs the once-per-process backup before any hosted service starts.</summary>
public sealed class BackendStartsicherungsHostedService(
    BackendStartsicherungsKoordinator coordinator,
    ILogger<BackendStartsicherungsHostedService> logger) : IHostedLifecycleService
{
    private static readonly Action<ILogger, BackendStartsicherungsStatus, Exception?> Completion =
        LoggerMessage.Define<BackendStartsicherungsStatus>(
            LogLevel.Information,
            new EventId(1, "BackendStartsicherung"),
            "Backend-Startsicherung beendet: {Status}");

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        var result = await coordinator.Ausfuehren(cancellationToken).ConfigureAwait(false);
        Completion(logger, result.Status, null);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
