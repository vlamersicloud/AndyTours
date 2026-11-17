namespace AndyTours.Tests.Sicherung;

using AndyTours.Api;
using AndyTours.Application.Koordination;
using Xunit;

public sealed class DatenbestandskoordinationTests
{
    [Fact]
    public async Task Exclusive_request_waits_for_normal_work_and_blocks_later_readers()
    {
        using var gate = new Datenbestandskoordination(2, 4);
        using var first = await gate.Betreten(default);
        var writerTask = gate.ExklusivBetreten(default).AsTask();
        Assert.False(writerTask.IsCompleted);
        var laterNormalTask = gate.Betreten(default).AsTask();
        Assert.False(laterNormalTask.IsCompleted);

        first.Dispose();
        using var writer = await writerTask;
        Assert.False(laterNormalTask.IsCompleted);
        writer.Dispose();
        using var later = await laterNormalTask;
    }

    [Fact]
    public async Task Queue_saturation_returns_fixed_busy_error_and_recovers()
    {
        using var gate = new Datenbestandskoordination(1, 1);
        using var holder = await gate.Betreten(default);
        var exclusiveTask = gate.ExklusivBetreten(default).AsTask();
        var error = await Assert.ThrowsAsync<DatenbestandBelegtException>(async () =>
            await gate.Betreten(default));
        Assert.Equal("Datenbestand ist vorübergehend belegt.", error.Message);

        holder.Dispose();
        using var exclusive = await exclusiveTask;
        exclusive.Dispose();
        using var recovered = await gate.Betreten(default);
    }

    [Fact]
    public async Task Cancellation_of_queued_exclusive_request_does_not_leak_capacity()
    {
        using var gate = new Datenbestandskoordination(1, 2);
        using var holder = await gate.Betreten(default);
        using var cts = new CancellationTokenSource();
        var pending = gate.ExklusivBetreten(cts.Token).AsTask();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        holder.Dispose();
        using var recovered = await gate.Betreten(default);
    }

    [Fact]
    public async Task Lease_disposal_is_idempotent_and_gate_disposal_rejects_new_work()
    {
        using var gate = new Datenbestandskoordination(1, 2);
        var lease = await gate.Betreten(default);
        lease.Dispose();
        lease.Dispose();
        using var recovered = await gate.Betreten(default);
        using var cts = new CancellationTokenSource();
        var queued = gate.Betreten(cts.Token).AsTask();
        Assert.False(queued.IsCompleted);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await queued);
        gate.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await gate.Betreten(default));
    }

    [Fact]
    public void Requires_explicit_positive_capacity_and_sufficient_queue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Datenbestandskoordination(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Datenbestandskoordination(2, 1));
    }
}
