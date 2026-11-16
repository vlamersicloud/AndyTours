using AndyTours.App.Funktionen;
using AndyTours.Application.Planung;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiPlanungsProfilSitzungTests
{
    [Fact]
    public async Task Coalesces_queued_intents_and_uses_confirmed_cas_version()
    {
        var port = new ControlledPort();
        using var session = new ApiPlanungsProfilSitzung(port, new(new string('A', 64), CourseFahrprofil.Rennrad));
        var first = session.Setzen(CourseFahrprofil.Trekking, default);
        await port.FirstEntered.Task;
        var stale = session.Setzen(CourseFahrprofil.Mountainbike, default);
        var latest = session.Setzen(CourseFahrprofil.Rennrad, default);
        port.FirstRelease.SetResult(new(new string('B', 64), CourseFahrprofil.Trekking));
        await Task.WhenAll(first, stale, latest);
        Assert.Equal(2, port.Writes.Count);
        Assert.Equal(new string('A', 64), port.Writes[0].Version);
        Assert.Equal(new string('B', 64), port.Writes[1].Version);
        Assert.Equal(CourseFahrprofil.Rennrad, port.Writes[1].Profil);
    }

    [Fact]
    public async Task Conflict_requires_manual_read_and_notifications_use_captured_context()
    {
        var context = new QueuedContext();
        var port = new ControlledPort { ConflictNext = true };
        using var session = new ApiPlanungsProfilSitzung(port, new(new string('A', 64), CourseFahrprofil.Rennrad), context);
        int events = 0; session.Geaendert += (_, _) => events++;
        var first = session.Setzen(CourseFahrprofil.Trekking, default);
        await context.Posted.Task; context.Drain(); await first;
        Assert.Single(port.Writes); Assert.Equal(0, port.Reads);
        var second = session.Setzen(CourseFahrprofil.Mountainbike, default);
        await context.Posted.Task; context.Drain(); await second;
        Assert.Equal(1, port.Reads); Assert.Equal(2, port.Writes.Count);
        Assert.True(events > 0);
        var queued = session.Setzen(CourseFahrprofil.Rennrad, default);
        await context.Posted.Task; session.Dispose(); context.Drain(); await queued;
        int previous = events; context.Drain(); Assert.Equal(previous, events);
    }

    private sealed class ControlledPort : IPlanungsProfil
    {
        public readonly TaskCompletionSource FirstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<PlanungsProfilAntwort> FirstRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly List<PlanungsProfilSpeichernAnfrage> Writes = [];
        public int Reads; public bool ConflictNext;
        public Task<PlanungsProfilAntwort> Laden(CancellationToken ct) { Reads++; return Task.FromResult(new PlanungsProfilAntwort(new string('C',64), CourseFahrprofil.Trekking)); }
        public Task<PlanungsProfilAntwort> Speichern(PlanungsProfilSpeichernAnfrage req, CancellationToken ct)
        {
            Writes.Add(req);
            if (Writes.Count == 1 && !ConflictNext) { FirstEntered.TrySetResult(); return FirstRelease.Task; }
            if (ConflictNext) { ConflictNext = false; throw new AndyTours.Application.Stammdaten.StammdatenException(AndyTours.Application.Stammdaten.StammdatenFehlerart.Konflikt, "safe"); }
            return Task.FromResult(new PlanungsProfilAntwort(new string('D',64), req.Profil));
        }
    }
    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();
        private TaskCompletionSource _posted = NewSignal();
        public TaskCompletionSource Posted => Volatile.Read(ref _posted);
        public override void Post(SendOrPostCallback d, object? state) { _queue.Enqueue((d,state)); _posted.TrySetResult(); }
        public void Drain() { while (_queue.TryDequeue(out var x)) x.Callback(x.State); Interlocked.Exchange(ref _posted, NewSignal()); }
        private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
