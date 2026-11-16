using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.App.ViewModels;

namespace AndyTours.Tests.Darstellung;

public sealed partial class ApiTourenMehrfachViewModelTests
{
    [Fact]
    public async Task Valid_ack_after_cancellation_remains_confirmed_and_is_not_resent()
    {
        using var lifetime = new CancellationTokenSource();
        var id = Guid.NewGuid();
        var fake = new LifecycleFake();
        fake.Activity = async (request, _) =>
        {
            await lifetime.CancelAsync();
            return ValidAnswer(request.AnfrageId, [1]);
        };
        using var vm = ApiTourenMehrfachViewModel.FuerAktivitaet(fake,
            new TourenAktivitaetAnfrage(id, [new(1, new string('A', 64))], 2), lifetime.Token);
        Assert.True(vm.KannAusfuehren);
        await vm.AusfuehrenCommand.ExecuteAsync(null);
        Assert.Equal(ApiTourenMehrfachStatus.Bestaetigt, vm.Status);
        Assert.False(vm.KannAusfuehren);
        Assert.False(vm.KannAbbrechen);
        await vm.AusfuehrenCommand.ExecuteAsync(null);
        Assert.Equal(1, fake.Sends);
    }

    [Fact]
    public async Task Dispose_while_busy_ignores_late_ack_and_property_notifications()
    {
        var id = Guid.NewGuid();
        var fake = new LifecycleFake();
        var release = new TaskCompletionSource<TourenMehrfachAntwort>(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.Activity = (_, _) => release.Task;
        var vm = ApiTourenMehrfachViewModel.FuerAktivitaet(fake,
            new TourenAktivitaetAnfrage(id, [new(1, new string('A', 64))], 2));
        var execution = vm.AusfuehrenCommand.ExecuteAsync(null);
        Assert.True(vm.IstBeschaeftigt);
        Assert.True(vm.KannAbbrechen);
        var notifications = 0;
        vm.PropertyChanged += (_, _) => notifications++;
        vm.Dispose();
        var afterDispose = notifications;
        Assert.False(vm.IstBeschaeftigt);
        Assert.False(vm.KannAbbrechen);
        vm.Dispose();
        release.SetResult(ValidAnswer(id, [1]));
        await execution;
        Assert.Equal(afterDispose, notifications);
        Assert.False(vm.IstBestaetigt);
        Assert.False(vm.IstBeschaeftigt);
    }

    [Fact]
    public async Task Abort_cancels_active_send_and_retry_is_explicit()
    {
        var id = Guid.NewGuid();
        var fake = new LifecycleFake();
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.Activity = async (request, ct) =>
        {
            using var registration = ct.Register(() => canceled.TrySetResult());
            if (fake.Sends == 1)
            {
                await canceled.Task;
                throw new OperationCanceledException(ct);
            }
            return ValidAnswer(request.AnfrageId, [1]);
        };
        using var vm = ApiTourenMehrfachViewModel.FuerAktivitaet(fake,
            new TourenAktivitaetAnfrage(id, [new(1, new string('A', 64))], 2));
        var execution = vm.AusfuehrenCommand.ExecuteAsync(null);
        Assert.True(vm.IstBeschaeftigt);
        Assert.True(vm.KannAbbrechen);
        vm.AbbrechenCommand.Execute(null);
        await execution;
        Assert.Equal(ApiTourenMehrfachStatus.Unklar, vm.Status);
        Assert.True(vm.KannAusfuehren);
        Assert.False(vm.KannAbbrechen);
        Assert.Equal(id, fake.LastActivityRequest!.AnfrageId);
        await vm.AusfuehrenCommand.ExecuteAsync(null);
        Assert.Equal(ApiTourenMehrfachStatus.Bestaetigt, vm.Status);
        Assert.Equal(2, fake.Sends);
    }

    private sealed class LifecycleFake : ITourenMehrfachverwaltung
    {
        public int Sends { get; private set; }
        public TourenAktivitaetAnfrage? LastActivityRequest { get; private set; }
        public Func<TourenAktivitaetAnfrage, CancellationToken, Task<TourenMehrfachAntwort>> Activity { get; set; } =
            (request, _) => Task.FromResult(ValidAnswer(request.AnfrageId, request.Touren.Select(x => x.Id).ToArray()));
        public Task<TourenMehrfachAntwort> Kategorien(TourenKategorienAnfrage request, CancellationToken ct) => throw new NotImplementedException();
        public Task<TourenMehrfachAntwort> Equipment(TourenEquipmentAnfrage request, CancellationToken ct) => throw new NotImplementedException();
        public Task<TourenMehrfachAntwort> Loeschen(TourenLoeschenAnfrage request, CancellationToken ct) => throw new NotImplementedException();
        public Task<TourenMehrfachAntwort> Aktivitaet(TourenAktivitaetAnfrage request, CancellationToken ct)
        {
            Sends++;
            LastActivityRequest = request;
            return Activity(request, ct);
        }
    }
}
