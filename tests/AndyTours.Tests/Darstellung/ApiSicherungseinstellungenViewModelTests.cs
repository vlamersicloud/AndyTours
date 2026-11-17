namespace AndyTours.Tests.Darstellung;

using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.App.ViewModels;
using AndyTours.Contracts.V1;
using Xunit;

public sealed class ApiSicherungseinstellungenViewModelTests
{
    private const string Version = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Load_enables_save_and_snapshot_precedes_busy_notification()
    {
        var fake = new FakePolicy();
        fake.Load = _ => Task.FromResult(new SicherungskonfigurationAntwort(Version, 3, 10, 4));
        fake.Save = (request, _) => Task.FromResult(new SicherungskonfigurationAntwort(Version, request.Intervall, request.Hoechstzahl, 5));
        using var vm = new ApiSicherungseinstellungenViewModel(fake);
        Assert.False(vm.SpeichernCommand.CanExecute(null));
        await vm.LadenAsync();
        Assert.True(vm.SpeichernCommand.CanExecute(null));
        vm.IntervallEntwurf = 7;
        vm.HoechstzahlEntwurf = 21;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.Aendert))
            {
                vm.IntervallEntwurf = 8;
                vm.HoechstzahlEntwurf = 22;
            }
        };
        await vm.SpeichernAsync();
        Assert.Equal(new SicherungskonfigurationAnfrage(Version, 7, 21), Assert.Single(fake.Saved));
    }

    [Fact]
    public async Task Conflict_and_failed_reload_keep_drafts_and_require_successful_reload()
    {
        var fake = new FakePolicy();
        fake.Load = _ => Task.FromResult(new SicherungskonfigurationAntwort(Version, 3, 10, 1));
        using var vm = new ApiSicherungseinstellungenViewModel(fake);
        await vm.LadenAsync();
        vm.IntervallEntwurf = 8;
        vm.HoechstzahlEntwurf = 24;
        fake.Save = (_, _) => Task.FromException<SicherungskonfigurationAntwort>(new StammdatenException(StammdatenFehlerart.Konflikt, "conflict"));
        await vm.SpeichernAsync();
        Assert.Equal(8, vm.IntervallEntwurf);
        Assert.Equal(24, vm.HoechstzahlEntwurf);
        Assert.False(vm.SpeichernCommand.CanExecute(null));
        fake.Load = _ => Task.FromException<SicherungskonfigurationAntwort>(new HttpRequestException());
        await vm.LadenAsync();
        Assert.Equal(8, vm.IntervallEntwurf);
        Assert.False(vm.SpeichernCommand.CanExecute(null));
        fake.Load = _ => Task.FromResult(new SicherungskonfigurationAntwort(Version, 5, 11, 2));
        await vm.LadenAsync();
        Assert.Equal(5, vm.IntervallEntwurf);
        Assert.True(vm.SpeichernCommand.CanExecute(null));
    }

    [Fact]
    public async Task Invalid_success_response_blocks_blind_retry()
    {
        var fake = new FakePolicy();
        fake.Load = _ => Task.FromResult(new SicherungskonfigurationAntwort(Version, 3, 10, 0));
        using var vm = new ApiSicherungseinstellungenViewModel(fake);
        await vm.LadenAsync();
        fake.Save = (_, _) => Task.FromResult(new SicherungskonfigurationAntwort("bad", 4, 10, 0));
        await vm.SpeichernAsync();
        Assert.False(vm.SpeichernCommand.CanExecute(null));
        Assert.Single(fake.Saved);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(99, true)]
    [InlineData(100, false)]
    public async Task Draft_bounds_control_validity_and_save(int value, bool expectedValid)
    {
        var fake = new FakePolicy { Load = _ => Task.FromResult(new SicherungskonfigurationAntwort(Version, 3, 10, 0)) };
        using var vm = new ApiSicherungseinstellungenViewModel(fake);
        await vm.LadenAsync();
        vm.IntervallEntwurf = value;
        Assert.Equal(expectedValid, vm.EingabenGueltig);
        Assert.Equal(expectedValid, vm.SpeichernCommand.CanExecute(null));
        vm.IntervallEntwurf = 3;
        vm.HoechstzahlEntwurf = value;
        Assert.Equal(expectedValid, vm.EingabenGueltig);
        Assert.Equal(expectedValid, vm.SpeichernCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Uncertain_or_cancelled_save_is_not_retried(bool cancelled)
    {
        var fake = new FakePolicy { Load = _ => Task.FromResult(new SicherungskonfigurationAntwort(Version, 3, 10, 0)) };
        using var vm = new ApiSicherungseinstellungenViewModel(fake);
        await vm.LadenAsync();
        vm.IntervallEntwurf = 8;
        vm.HoechstzahlEntwurf = 17;
        fake.Save = (_, _) => Task.FromException<SicherungskonfigurationAntwort>(
            cancelled ? new OperationCanceledException() : new HttpRequestException());
        await vm.SpeichernAsync();
        Assert.Single(fake.Saved);
        Assert.False(vm.SpeichernCommand.CanExecute(null));
        Assert.Equal(8, vm.IntervallEntwurf);
        Assert.Equal(17, vm.HoechstzahlEntwurf);
    }

    [Fact]
    public async Task Closing_while_load_is_pending_prevents_late_apply()
    {
        var completion = new TaskCompletionSource<SicherungskonfigurationAntwort>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new FakePolicy { Load = _ => completion.Task };
        using var vm = new ApiSicherungseinstellungenViewModel(fake);
        var pending = vm.LadenAsync();
        vm.BeginClose();
        completion.SetResult(new(Version, 9, 9, 9));
        await pending;
        Assert.Equal(3, vm.IntervallEntwurf);
        Assert.Equal(10, vm.HoechstzahlEntwurf);
        Assert.Equal(0, vm.StartsSeitSicherung);
    }

    private sealed class FakePolicy : ISicherungskonfiguration
    {
        public Func<CancellationToken, Task<SicherungskonfigurationAntwort>> Load { get; set; } =
            _ => Task.FromResult(new SicherungskonfigurationAntwort(Version, 3, 10, 0));
        public Func<SicherungskonfigurationAnfrage, CancellationToken, Task<SicherungskonfigurationAntwort>> Save { get; set; } =
            (_, _) => Task.FromResult(new SicherungskonfigurationAntwort(Version, 3, 10, 0));
        public List<SicherungskonfigurationAnfrage> Saved { get; } = [];
        public Task<SicherungskonfigurationAntwort> Laden(CancellationToken cancellationToken) => Load(cancellationToken);
        public Task<SicherungskonfigurationAntwort> Speichern(SicherungskonfigurationAnfrage request, CancellationToken cancellationToken)
        {
            Saved.Add(request);
            return Save(request, cancellationToken);
        }
    }
}
