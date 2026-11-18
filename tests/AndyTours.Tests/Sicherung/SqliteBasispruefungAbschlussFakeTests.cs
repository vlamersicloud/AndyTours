namespace AndyTours.Tests.Sicherung;

using AndyTours.App.ViewModels;
using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;
using Xunit;

public sealed class SqliteBasispruefungAbschlussFakeTests
{
    [Fact]
    public async Task DisposeCancelsPendingCheckAndIgnoresLateResult()
    {
        var entry = new SicherungAntwort(Guid.NewGuid(), Sicherungsart.Vollarchiv, DateTimeOffset.UtcNow, 1, new string('a', 64));
        var port = new PendingPort(); var vm = new ApiWiederherstellungsvorschauViewModel(new Catalog(entry), new NoPreview(), port);
        try
        {
            await vm.LadenAsync(); vm.Auswahl = vm.Eintraege[0];
            var checking = vm.SqlitePruefenAsync(); await port.Started.Task;
            vm.Dispose(); Assert.True(port.Token.IsCancellationRequested);
            port.Completion.TrySetResult(new(entry.Id, "ok", 1, 8, true, Array.Empty<string>()));
            await checking; Assert.Null(vm.SqliteErgebnis);
        }
        finally
        {
            port.Completion.TrySetResult(new(entry.Id, "ok", 1, 8, true, Array.Empty<string>()));
            vm.Dispose();
        }
    }
    private sealed class PendingPort : ISqliteBasispruefung
    {
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<SqliteBasispruefungAntwort> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token;
        public Task<SqliteBasispruefungAntwort> Pruefen(SqliteBasispruefungAnfrage anfrage, CancellationToken cancellationToken)
        { Token = cancellationToken; Started.TrySetResult(); return Completion.Task; }
    }
    private sealed class Catalog(SicherungAntwort item) : ISicherungskatalog
    { public Task<SicherungListeAntwort> Liste(CancellationToken ct) => Task.FromResult(new SicherungListeAntwort(new[] { item })); }
    private sealed class NoPreview : IWiederherstellungsvorschau
    { public Task<WiederherstellungsvorschauAntwort> Pruefen(WiederherstellungsvorschauAnfrage anfrage, CancellationToken ct) => throw new InvalidOperationException(); }
}
