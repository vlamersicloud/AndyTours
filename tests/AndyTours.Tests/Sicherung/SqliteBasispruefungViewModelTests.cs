namespace AndyTours.Tests.Sicherung;

using AndyTours.App.ViewModels;
using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;
using Xunit;

public sealed class SqliteBasispruefungViewModelTests
{
    [Fact]
    public async Task DoesNotAutocheckAndRejectsNonFullSelection()
    {
        var port = new FakePort(); using var vm = new ApiWiederherstellungsvorschauViewModel(new LegacyCatalog(), new FakePreview(), port);
        Assert.Equal("SQLite-Basisprüfung nicht ausgeführt.", vm.SqliteStatus);
        await vm.LadenAsync();
        vm.Auswahl = vm.Eintraege[0];
        Assert.False(vm.SqlitePruefenCommand.CanExecute(null));
        Assert.Equal(0, port.Calls);
    }
    [Fact]
    public async Task SelectionChangeDropsLateResult()
    {
        var catalog = new FullCatalog(); var port = new FakePort(); using var vm = new ApiWiederherstellungsvorschauViewModel(catalog, new FakePreview(), port);
        await vm.LadenAsync(); vm.Auswahl = vm.Eintraege[0];
        var pending = vm.SqlitePruefenAsync();
        vm.Auswahl = vm.Eintraege[1];
        port.Completion.TrySetResult(new(vm.Eintraege[0].Id, "ok", 1, 8, true, Array.Empty<string>()));
        await pending;
        Assert.Null(vm.SqliteErgebnis);
    }
    private sealed class FakePort : ISqliteBasispruefung
    {
        public int Calls; public TaskCompletionSource<SqliteBasispruefungAntwort> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<SqliteBasispruefungAntwort> Pruefen(SqliteBasispruefungAnfrage a, CancellationToken ct) { Calls++; return Completion.Task; }
    }
    private sealed class LegacyCatalog : ISicherungskatalog
    {
        public Task<SicherungListeAntwort> Liste(CancellationToken ct) => Task.FromResult(new SicherungListeAntwort(new[]
        { new SicherungAntwort(Guid.NewGuid(), Sicherungsart.LegacySqlite, null, 1, new string('b',64)) { LegacyZeitstempel = "2024-02-29_12-34-56" } }));
    }
    private sealed class FullCatalog : ISicherungskatalog
    {
        private readonly SicherungAntwort[] _items = { Entry(), Entry() };
        private static SicherungAntwort Entry() => new(Guid.NewGuid(), Sicherungsart.Vollarchiv, DateTimeOffset.UtcNow, 1, new string('a',64));
        public Task<SicherungListeAntwort> Liste(CancellationToken ct) => Task.FromResult(new SicherungListeAntwort(_items));
    }
    private sealed class FakePreview : IWiederherstellungsvorschau
    {
        public Task<WiederherstellungsvorschauAntwort> Pruefen(WiederherstellungsvorschauAnfrage a, CancellationToken ct) => throw new InvalidOperationException();
    }
}
