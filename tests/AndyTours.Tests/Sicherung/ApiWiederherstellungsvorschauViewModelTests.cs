namespace AndyTours.Tests.Sicherung;

using AndyTours.App.ViewModels;
using System.Net;
using System.ComponentModel;
using System.Threading;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Xunit;

public sealed class ApiWiederherstellungsvorschauViewModelTests
{
    private static readonly Guid FullId = Guid.Parse("4e1f6d1c-1284-4a7e-a95b-2e7b6215ed10");
    private static readonly Guid LegacyId = Guid.Parse("5e1f6d1c-1284-4a7e-a95b-2e7b6215ed11");
    private static readonly DateTimeOffset Utc = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task LaedtUnveraenderlichenSnapshotUndFordertVorschauMitExakterIdentitaetAn()
    {
        var full = FullEntry();
        var legacy = new SicherungAntwort(LegacyId, Sicherungsart.LegacySqlite, null, 456, Hash) { LegacyZeitstempel = "2024-02-29_12-34-56" };
        var source = new List<SicherungAntwort> { full, legacy };
        var catalog = new FakeCatalog(() => Task.FromResult(new SicherungListeAntwort(source)));
        var preview = new FakePreview((request, _) => Task.FromResult(FullAnswer(full)));
        using var vm = new ApiWiederherstellungsvorschauViewModel(catalog, preview);

        await vm.LadenAsync();
        source.Clear();
        Assert.Equal(2, vm.Eintraege.Count);
        Assert.True(((IList<SicherungAntwort>)vm.Eintraege).IsReadOnly);
        Assert.False(vm.MussNeuLaden);
        Assert.Null(vm.Auswahl);
        Assert.Null(vm.Vorschau);

        vm.Auswahl = full;
        await vm.VorschauAsync();
        Assert.Equal(new WiederherstellungsvorschauAnfrage(FullId, Hash), preview.LastRequest);
        Assert.NotNull(vm.Vorschau);
        Assert.Null(vm.Vorschau!.Dateianzahl);
        Assert.Null(vm.Vorschau.NutzdatenBytes);
        Assert.False(vm.Vorschau.AnwendungFreigegeben);
        Assert.Equal(1, catalog.Calls);
        Assert.Equal(1, preview.Calls);
    }

    [Fact]
    public async Task LegacyPreviewHasKnownCountAndBytesOnly()
    {
        var legacy = new SicherungAntwort(LegacyId, Sicherungsart.LegacySqlite, null, 456, Hash) { LegacyZeitstempel = "2024-02-29_12-34-56" };
        var preview = new FakePreview((_, _) => Task.FromResult(new WiederherstellungsvorschauAntwort(legacy, null, 1, 456, false, false, ["Legacy DB only."])));
        using var vm = new ApiWiederherstellungsvorschauViewModel(new FakeCatalog(() => Task.FromResult(new SicherungListeAntwort([legacy]))), preview);
        await vm.LadenAsync(); vm.Auswahl = legacy; await vm.VorschauAsync();
        Assert.Equal(1, vm.Vorschau!.Dateianzahl);
        Assert.Equal(456L, vm.Vorschau.NutzdatenBytes);
        Assert.False(vm.Vorschau.AnwendungFreigegeben);
    }

    [Fact]
    public async Task OhneAuswahlWirdPreviewPortNichtAufgerufen()
    {
        var preview = new FakePreview((_, _) => Task.FromResult(FullAnswer(FullEntry())));
        using var vm = new ApiWiederherstellungsvorschauViewModel(new FakeCatalog(() => Task.FromResult(new SicherungListeAntwort([FullEntry()]))), preview);
        await vm.LadenAsync(); await vm.VorschauAsync();
        Assert.Equal(0, preview.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UngueltigeOderDoppelteKatalogdatenBlockierenPreview(bool duplicate)
    {
        var item = FullEntry();
        var rows = duplicate ? new[] { item, item } : new[] { item with { InhaltHash = "bad" } };
        var preview = new FakePreview((_, _) => Task.FromResult(FullAnswer(item)));
        using var vm = new ApiWiederherstellungsvorschauViewModel(new FakeCatalog(() => Task.FromResult(new SicherungListeAntwort(rows))), preview);
        await vm.LadenAsync();
        Assert.True(vm.MussNeuLaden);
        Assert.Empty(vm.Eintraege);
        vm.Auswahl = item; await vm.VorschauAsync();
        Assert.Equal(0, preview.Calls);
    }

    [Fact]
    public async Task InvalidResponseIsNotPublished()
    {
        var item = FullEntry();
        var invalid = FullAnswer(item) with { AnwendungFreigegeben = true };
        using var vm = new ApiWiederherstellungsvorschauViewModel(new FakeCatalog(() => Task.FromResult(new SicherungListeAntwort([item]))), new FakePreview((_, _) => Task.FromResult(invalid)));
        await vm.LadenAsync(); vm.Auswahl = item; await vm.VorschauAsync();
        Assert.Null(vm.Vorschau);
        Assert.Contains("ungültig", vm.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(StammdatenFehlerart.NichtGefunden)]
    [InlineData(StammdatenFehlerart.Konflikt)]
    public async Task CurrentNotFoundOrConflictRequiresReload(StammdatenFehlerart kind)
    {
        var item = FullEntry();
        using var vm = new ApiWiederherstellungsvorschauViewModel(new FakeCatalog(() => Task.FromResult(new SicherungListeAntwort([item]))), new FakePreview((_, _) => Task.FromException<WiederherstellungsvorschauAntwort>(new StammdatenException(kind, "safe"))));
        await vm.LadenAsync(); vm.Auswahl = item; await vm.VorschauAsync();
        Assert.True(vm.MussNeuLaden);
        Assert.Null(vm.Vorschau);
        await vm.LadenAsync();
        Assert.False(vm.MussNeuLaden);
    }

    [Fact]
    public async Task AuswahlwechselIgnoriertVerspaetetenErfolgUndKonflikt()
    {
        var a = FullEntry();
        var b = new SicherungAntwort(LegacyId, Sicherungsart.Vollarchiv, Utc, 321, Hash);
        var pendingSuccess = new TaskCompletionSource<WiederherstellungsvorschauAntwort>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingConflict = new TaskCompletionSource<WiederherstellungsvorschauAntwort>(TaskCreationOptions.RunContinuationsAsynchronously);
        var preview = new FakePreview((request, _) => request.SicherungsId == FullId ? pendingSuccess.Task : pendingConflict.Task);
        using var vm = new ApiWiederherstellungsvorschauViewModel(new FakeCatalog(() => Task.FromResult(new SicherungListeAntwort([a, b]))), preview);
        try
        {
            await vm.LadenAsync(); vm.Auswahl = a;
            var successTask = vm.VorschauAsync();
            vm.Auswahl = b;
            pendingSuccess.SetResult(FullAnswer(a));
            await successTask;
            Assert.Null(vm.Vorschau);
            Assert.False(vm.MussNeuLaden);

            var conflictTask = vm.VorschauAsync();
            vm.Auswahl = a;
            pendingConflict.SetException(new StammdatenException(StammdatenFehlerart.Konflikt, "safe"));
            await conflictTask;
            Assert.Null(vm.Vorschau);
            Assert.False(vm.MussNeuLaden);
            Assert.Contains("Auswahl geändert", vm.Status);
        }
        finally
        {
            pendingSuccess.TrySetResult(FullAnswer(a));
            pendingConflict.TrySetException(new StammdatenException(StammdatenFehlerart.Konflikt, "safe"));
        }
    }

    [Fact]
    public async Task DirekterParallelerPreviewAufrufSendetNurEinmal()
    {
        var item = FullEntry();
        var gate = new TaskCompletionSource<WiederherstellungsvorschauAntwort>(TaskCreationOptions.RunContinuationsAsynchronously);
        var preview = new FakePreview((_, _) => gate.Task);
        using var vm = new ApiWiederherstellungsvorschauViewModel(new FakeCatalog(() => Task.FromResult(new SicherungListeAntwort([item]))), preview);
        await vm.LadenAsync(); vm.Auswahl = item;
        var first = vm.VorschauAsync();
        try
        {
            await vm.VorschauAsync();
            Assert.Equal(1, preview.Calls);
        }
        finally { gate.TrySetResult(FullAnswer(item)); }
        await first;
    }

    [Fact]
    public async Task VorabAbbruchRuftKeinePortsAuf()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        var catalog = new FakeCatalog(() => Task.FromResult(new SicherungListeAntwort([FullEntry()])));
        var preview = new FakePreview((_, _) => Task.FromResult(FullAnswer(FullEntry())));
        using var vm = new ApiWiederherstellungsvorschauViewModel(catalog, preview);
        await vm.LadenAsync(cts.Token);
        Assert.Equal(0, catalog.Calls);
        Assert.Equal(0, preview.Calls);
    }

    [Fact]
    public async Task DisposeWaerendVorschauVerhindertSpaeterePropertyAenderungen()
    {
        var item = FullEntry();
        var gate = new TaskCompletionSource<WiederherstellungsvorschauAntwort>(TaskCreationOptions.RunContinuationsAsynchronously);
        var preview = new FakePreview((_, _) => gate.Task);
        var vm = new ApiWiederherstellungsvorschauViewModel(new FakeCatalog(() => Task.FromResult(new SicherungListeAntwort([item]))), preview);
        await vm.LadenAsync(); vm.Auswahl = item;
        var changes = 0;
        vm.PropertyChanged += Count;
        var task = vm.VorschauAsync();
        try
        {
            vm.Dispose();
            changes = 0;
            gate.SetResult(FullAnswer(item));
            await task;
            Assert.Equal(0, changes);
        }
        finally { gate.TrySetResult(FullAnswer(item)); vm.Dispose(); }
        void Count(object? sender, PropertyChangedEventArgs args) => changes++;
    }

    [Fact]
    public async Task DisposeWaerendLadenVerhindertSpaeterePropertyAenderungen()
    {
        var gate = new TaskCompletionSource<SicherungListeAntwort>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new ApiWiederherstellungsvorschauViewModel(new FakeCatalog(() => gate.Task), new FakePreview((_, _) => Task.FromResult(FullAnswer(FullEntry()))));
        var changes = 0;
        var task = vm.LadenAsync();
        try
        {
            vm.PropertyChanged += Count;
            vm.Dispose();
            changes = 0;
            gate.SetResult(new SicherungListeAntwort([FullEntry()]));
            await task;
            Assert.Equal(0, changes);
        }
        finally { gate.TrySetResult(new SicherungListeAntwort([FullEntry()])); vm.Dispose(); }
        void Count(object? sender, PropertyChangedEventArgs args) => changes++;
    }

    [Fact]
    public async Task VorabAbbruchPreviewRuftPortNicht()
    {
        var item = FullEntry();
        var preview = new FakePreview((_, _) => Task.FromResult(FullAnswer(item)));
        using var vm = new ApiWiederherstellungsvorschauViewModel(new FakeCatalog(() => Task.FromResult(new SicherungListeAntwort([item]))), preview);
        await vm.LadenAsync(); vm.Auswahl = item;
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await vm.VorschauAsync(cts.Token);
        Assert.Equal(0, preview.Calls);
    }

    [Fact]
    public async Task ServiceUnavailableStatusIstSicher()
    {
        var item = FullEntry();
        var preview = new FakePreview((_, _) => Task.FromException<WiederherstellungsvorschauAntwort>(
            new HttpRequestException("PRIVATE path", null, HttpStatusCode.ServiceUnavailable)));
        using var vm = new ApiWiederherstellungsvorschauViewModel(new FakeCatalog(() => Task.FromResult(new SicherungListeAntwort([item]))), preview);
        await vm.LadenAsync(); vm.Auswahl = item; await vm.VorschauAsync();
        Assert.DoesNotContain("PRIVATE", vm.Status);
        Assert.False(vm.MussNeuLaden);
    }

    private static SicherungAntwort FullEntry() => new(FullId, Sicherungsart.Vollarchiv, Utc, 123, Hash);
    private static WiederherstellungsvorschauAntwort FullAnswer(SicherungAntwort item) => new(item, null, null, null, false, false, ["Metadata only."]);

    private sealed class FakeCatalog(Func<Task<SicherungListeAntwort>> reply) : ISicherungskatalog
    {
        public int Calls { get; private set; }
        public Task<SicherungListeAntwort> Liste(CancellationToken cancellationToken) { Calls++; cancellationToken.ThrowIfCancellationRequested(); return reply(); }
    }

    private sealed class FakePreview(Func<WiederherstellungsvorschauAnfrage, CancellationToken, Task<WiederherstellungsvorschauAntwort>> reply) : IWiederherstellungsvorschau
    {
        public int Calls { get; private set; }
        public WiederherstellungsvorschauAnfrage? LastRequest { get; private set; }
        public Task<WiederherstellungsvorschauAntwort> Pruefen(WiederherstellungsvorschauAnfrage anfrage, CancellationToken cancellationToken)
        { Calls++; LastRequest = anfrage; cancellationToken.ThrowIfCancellationRequested(); return reply(anfrage, cancellationToken); }
    }
}
