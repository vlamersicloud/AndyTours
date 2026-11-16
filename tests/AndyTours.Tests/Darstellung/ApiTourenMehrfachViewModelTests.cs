using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.App.ViewModels;

namespace AndyTours.Tests.Darstellung;

public sealed partial class ApiTourenMehrfachViewModelTests
{
    [Fact]
    public async Task Unknown_send_retries_same_normalized_request_and_confirms_once()
    {
        var id = Guid.NewGuid();
        var refs = new List<TourVersionsreferenz> { new(8, new string('b', 64)), new(2, new string('a', 64)) };
        var fake = new FakeService();
        fake.Reply = request => fake.ActivityRequests.Count == 1
            ? throw new HttpRequestException("sentinel")
            : Task.FromResult(ValidAnswer(((TourenAktivitaetAnfrage)request).AnfrageId, [2, 8]));
        using var vm = ApiTourenMehrfachViewModel.FuerAktivitaet(fake, new TourenAktivitaetAnfrage(id, refs, 4));
        refs.Clear();

        await vm.AusfuehrenCommand.ExecuteAsync(null);
        Assert.Equal(ApiTourenMehrfachStatus.Unklar, vm.Status);
        Assert.NotNull(vm.Hinweis);
        await vm.AusfuehrenCommand.ExecuteAsync(null);
        Assert.Equal(ApiTourenMehrfachStatus.Bestaetigt, vm.Status);
        Assert.Equal(2, fake.ActivityRequests.Count);
        Assert.Equal(id, fake.ActivityRequests[0].AnfrageId);
        Assert.Same(fake.ActivityRequests[0], fake.ActivityRequests[1]);
        Assert.Equal(new long[] { 2, 8 }, fake.ActivityRequests[0].Touren.Select(x => x.Id));
        Assert.Equal(new[] { new string('A', 64), new string('B', 64) }, fake.ActivityRequests[0].Touren.Select(x => x.Version));
        await vm.AusfuehrenCommand.ExecuteAsync(null);
        Assert.Equal(2, fake.ActivityRequests.Count);
        Assert.NotNull(vm.Antwort);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("missing")]
    [InlineData("conflict")]
    public async Task Definitive_domain_errors_are_rejected_without_echoing(string failure)
    {
        var fake = new FakeService
        {
            Reply = _ => failure switch
            {
                "invalid" => throw new UngueltigeTourException("secret"),
                "missing" => throw new TourNichtGefundenException(99),
                _ => throw new TourVersionskonfliktException()
            }
        };
        using var vm = ApiTourenMehrfachViewModel.FuerKategorien(fake,
            new TourenKategorienAnfrage(Guid.NewGuid(), [new(1, new string('A', 64))], [], []));
        await vm.AusfuehrenCommand.ExecuteAsync(null);
        Assert.Equal(ApiTourenMehrfachStatus.Abgelehnt, vm.Status);
        Assert.DoesNotContain("secret", vm.Hinweis);
        await vm.AusfuehrenCommand.ExecuteAsync(null);
        Assert.Single(fake.CategoryRequests);
    }

    [Fact]
    public async Task Malformed_ack_is_unknown_then_valid_ack_notifies_answer()
    {
        var id = Guid.NewGuid();
        var fake = new FakeService { Reply = _ => Task.FromResult(new TourenMehrfachAntwort(Guid.NewGuid(), [], [])) };
        using var vm = ApiTourenMehrfachViewModel.FuerEquipment(fake,
            new TourenEquipmentAnfrage(id, [new(1, new string('A', 64))], null));
        var observedHint = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Hinweis) && vm.Hinweis is not null) observedHint = true;
        };
        await vm.AusfuehrenCommand.ExecuteAsync(null);
        Assert.Equal(ApiTourenMehrfachStatus.Unklar, vm.Status);
        Assert.True(observedHint);

        var observedAnswer = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Antwort) && vm.Antwort is not null) observedAnswer = true;
        };
        fake.Reply = _ => Task.FromResult(ValidAnswer(id, [1]));
        await vm.AusfuehrenCommand.ExecuteAsync(null);
        Assert.Equal(ApiTourenMehrfachStatus.Bestaetigt, vm.Status);
        Assert.True(observedAnswer);
    }

    private static TourenMehrfachAntwort ValidAnswer(Guid id, long[] ids)
    {
        var values = TourenMapper.ToAntwort(new Trainingseinheit
        {
            Datum = new DateOnly(2026, 1, 1), Name = "Tour", AktivitaetId = 1, FahrerId = 1
        }).Werte;
        var rows = ids.Select((tourId, index) =>
            new TourAntwort(tourId, new string((char)('A' + index), 64), values)).ToArray();
        return new TourenMehrfachAntwort(id, Array.AsReadOnly(rows), Array.AsReadOnly(Array.Empty<long>()));
    }

    private sealed class FakeService : ITourenMehrfachverwaltung
    {
        public List<TourenKategorienAnfrage> CategoryRequests { get; } = [];
        public List<TourenAktivitaetAnfrage> ActivityRequests { get; } = [];
        public Func<object, Task<TourenMehrfachAntwort>> Reply { get; set; } =
            _ => Task.FromResult(new TourenMehrfachAntwort(Guid.Empty, [], []));
        public Task<TourenMehrfachAntwort> Kategorien(TourenKategorienAnfrage anfrage, CancellationToken ct)
        {
            CategoryRequests.Add(anfrage);
            return Reply(anfrage);
        }
        public Task<TourenMehrfachAntwort> Aktivitaet(TourenAktivitaetAnfrage anfrage, CancellationToken ct)
        {
            ActivityRequests.Add(anfrage);
            return Reply(anfrage);
        }
        public Task<TourenMehrfachAntwort> Equipment(TourenEquipmentAnfrage anfrage, CancellationToken ct) => Reply(anfrage);
        public Task<TourenMehrfachAntwort> Loeschen(TourenLoeschenAnfrage anfrage, CancellationToken ct) => Reply(anfrage);
    }
}
