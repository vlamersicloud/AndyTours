using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.App.ViewModels;

namespace AndyTours.Tests.Presentation;

public sealed class ApiTextlisteNutzungViewModelTests
{
    private const string Version = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string Stamp = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
    private static readonly string[] InitialEntries = ["Kette"];

    [Fact]
    public async Task RenameNeedsUsageConfirmationAndUsesExactPreviewStamp()
    {
        var port = new FakeTextlistenPort
        {
            Usage = new(Textlistenart.Taetigkeit, Version, "Kette", 0, 2, 1, Stamp)
        };
        var accepted = false;
        string? prompt = null;
        using var vm = new ApiTextlisteViewModel(Textlistenart.Taetigkeit, port, text => { prompt = text; return accepted; });
        await vm.LadenAsync();
        vm.Auswahl = "Kette";
        vm.NameText = "Antrieb";
        await vm.UmbenennenCommand.ExecuteAsync(null);
        Assert.Equal(0, port.RenameCalls);
        Assert.Contains("2 Wartungen und 1 Alarm", prompt ?? string.Empty);

        accepted = true;
        await vm.UmbenennenCommand.ExecuteAsync(null);
        Assert.Equal(1, port.RenameCalls);
        Assert.Equal(Stamp, port.LastRename?.VerwendungsVersion);
    }

    [Fact]
    public async Task DeleteWithUsageShowsCountsWithoutDeleteCall()
    {
        var port = new FakeTextlistenPort
        {
            Usage = new(Textlistenart.Taetigkeit, Version, "Kette", 0, 1, 2, Stamp)
        };
        using var vm = new ApiTextlisteViewModel(Textlistenart.Taetigkeit, port, _ => true);
        await vm.LadenAsync();
        vm.Auswahl = "Kette";
        await vm.LoeschenCommand.ExecuteAsync(null);
        Assert.Equal(0, port.DeleteCalls);
        Assert.Contains("1 Wartung und 2 Alarme", vm.Status);
    }

    [Fact]
    public async Task UnusedDeleteStillRequiresConfirmationAndRejectSendsNothing()
    {
        var port = new FakeTextlistenPort
        {
            Usage = new(Textlistenart.Taetigkeit, Version, "Kette", 0, 0, 0, Stamp)
        };
        var confirm = false;
        using var vm = new ApiTextlisteViewModel(Textlistenart.Taetigkeit, port, _ => confirm);
        await vm.LadenAsync();
        vm.Auswahl = "Kette";
        await vm.LoeschenCommand.ExecuteAsync(null);
        Assert.Equal(0, port.DeleteCalls);
        confirm = true;
        await vm.LoeschenCommand.ExecuteAsync(null);
        Assert.Equal(1, port.DeleteCalls);
    }

    [Fact]
    public async Task SelectionOrNameChangeDuringPreviewNeverSendsRename()
    {
        foreach (bool changeSelection in new[] { false, true })
        {
            var port = new FakeTextlistenPort { PendingUsage = new TaskCompletionSource<TextlistenNutzungAntwort>(TaskCreationOptions.RunContinuationsAsynchronously) };
            using var vm = new ApiTextlisteViewModel(Textlistenart.Taetigkeit, port, _ => true);
            await vm.LadenAsync();
            vm.Auswahl = "Kette";
            vm.NameText = "Antrieb";
            Task? operation = null;
            try
            {
                operation = vm.UmbenennenCommand.ExecuteAsync(null);
                await port.UsageStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
                if (changeSelection) vm.Auswahl = null;
                else vm.NameText = "Andere";
                port.PendingUsage.SetResult(new(Textlistenart.Taetigkeit, Version, "Kette", 0, 1, 0, Stamp));
                await operation;
                Assert.Equal(0, port.RenameCalls);
            }
            finally
            {
                port.PendingUsage.TrySetResult(new(Textlistenart.Taetigkeit, Version, "Kette", 0, 1, 0, Stamp));
                if (operation is not null) await operation.WaitAsync(TimeSpan.FromSeconds(2));
            }
        }
    }

    [Fact]
    public async Task ConflictLocksWritesUntilSuccessfulReload()
    {
        var port = new FakeTextlistenPort { RenameFailure = new StammdatenException(StammdatenFehlerart.Konflikt, "conflict") };
        using var vm = new ApiTextlisteViewModel(Textlistenart.Taetigkeit, port, _ => true);
        await vm.LadenAsync();
        vm.Auswahl = "Kette";
        vm.NameText = "Antrieb";
        await vm.UmbenennenCommand.ExecuteAsync(null);
        await vm.UmbenennenCommand.ExecuteAsync(null);
        Assert.Equal(1, port.RenameCalls);
        port.RenameFailure = null;
        await vm.LadenAsync();
        vm.Auswahl = "Kette";
        vm.NameText = "Antrieb";
        await vm.UmbenennenCommand.ExecuteAsync(null);
        Assert.Equal(2, port.RenameCalls);
    }

    [Fact]
    public async Task UnknownWriteAndMalformedAckRemainLockedUntilReload()
    {
        var port = new FakeTextlistenPort { RenameFailure = new IOException("transport") };
        using var vm = new ApiTextlisteViewModel(Textlistenart.Taetigkeit, port, _ => true);
        await vm.LadenAsync();
        vm.Auswahl = "Kette";
        vm.NameText = "Antrieb";
        await vm.UmbenennenCommand.ExecuteAsync(null);
        await vm.UmbenennenCommand.ExecuteAsync(null);
        Assert.Equal(1, port.RenameCalls);
        Assert.Contains("Speicherstatus unklar", vm.Status);

        port.RenameFailure = null;
        port.RenameResponse = new TextlisteAntwort(Textlistenart.Taetigkeit, Version, ["dup", "dup"]);
        await vm.LadenAsync();
        vm.Auswahl = "Kette";
        vm.NameText = "Antrieb";
        await vm.UmbenennenCommand.ExecuteAsync(null);
        Assert.Equal(2, port.RenameCalls);
        Assert.Contains("Speicherstatus unklar", vm.Status);
        Assert.Equal(InitialEntries, vm.Eintraege);
    }

    [Fact]
    public async Task LateUsageAfterCloseNeverStartsRename()
    {
        var port = new FakeTextlistenPort { PendingUsage = new TaskCompletionSource<TextlistenNutzungAntwort>(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var vm = new ApiTextlisteViewModel(Textlistenart.Taetigkeit, port, _ => true);
        await vm.LadenAsync();
        vm.Auswahl = "Kette";
        vm.NameText = "Antrieb";
        Task? operation = null;
        try
        {
            operation = vm.UmbenennenCommand.ExecuteAsync(null);
            await port.UsageStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            vm.Dispose();
            port.PendingUsage.SetResult(new(Textlistenart.Taetigkeit, Version, "Kette", 0, 1, 0, Stamp));
            await operation;
            Assert.Equal(0, port.RenameCalls);
        }
        finally
        {
            port.PendingUsage.TrySetResult(new(Textlistenart.Taetigkeit, Version, "Kette", 0, 1, 0, Stamp));
            if (operation is not null) await operation.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public void EndpointPortMarkupAndApplicationConfirmationAreWired()
    {
        static string Resource(string name)
        {
            using var stream = typeof(ApiTextlisteNutzungViewModelTests).Assembly.GetManifestResourceStream(name);
            Assert.NotNull(stream);
            using var reader = new StreamReader(stream!);
            return reader.ReadToEnd();
        }

        var endpoint = Resource("AndyTours.TextlistenEndpunkte.cs");
        var port = Resource("AndyTours.ITextlistenVerwaltung.cs");
        var markup = Resource("AndyTours.ApiTextlisteFenster.xaml");
        var app = Resource("AndyTours.App.Api.cs");
        Assert.Contains("/nutzung", endpoint);
        Assert.Contains("NutzungLesen", endpoint);
        Assert.Contains("NutzungLesen", port);
        Assert.Contains("LoeschenCommand", markup);
        Assert.Contains("YesNo", app);
        Assert.Contains("MessageBoxResult.No", app);
        Assert.Contains("TextlisteVerwaltenAngefordert", app);
    }

    private sealed class FakeTextlistenPort : ITextlistenVerwaltung
    {
        public TextlistenNutzungAntwort Usage { get; init; } = new(Textlistenart.Taetigkeit, Version, "Kette", 0, 0, 0, Stamp);
        public TaskCompletionSource<TextlistenNutzungAntwort>? PendingUsage { get; init; }
        public TaskCompletionSource UsageStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RenameCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public Exception? RenameFailure { get; set; }
        public TextlisteAntwort? RenameResponse { get; set; }
        public TextlisteUmbenennenAnfrage? LastRename { get; private set; }

        public Task<TextlisteAntwort> Laden(Textlistenart art, CancellationToken ct = default) =>
            Task.FromResult(new TextlisteAntwort(art, Version, ["Kette"]));

        public async Task<TextlistenNutzungAntwort> NutzungLesen(Textlistenart art, string listenVersion, string name, CancellationToken ct = default)
        {
            if (PendingUsage is null) return Usage;
            UsageStarted.TrySetResult();
            return await PendingUsage.Task;
        }

        public Task<TextlisteAntwort> Umbenennen(Textlistenart art, TextlisteUmbenennenAnfrage anfrage, CancellationToken ct = default)
        {
            RenameCalls++;
            LastRename = anfrage;
            if (RenameFailure is not null) throw RenameFailure;
            return Task.FromResult(RenameResponse ?? new TextlisteAntwort(art, Version, ["Antrieb"]));
        }

        public Task<TextlisteAntwort> Anlegen(Textlistenart art, TextlisteneintragAnfrage anfrage, CancellationToken ct = default)
        {
            throw new NotSupportedException("Anlegen wird in dieser Fakefixture nicht verwendet.");
        }
        public Task<TextlisteAntwort> Loeschen(Textlistenart art, TextlisteneintragAnfrage anfrage, CancellationToken ct = default)
        {
            DeleteCalls++;
            return Task.FromResult(new TextlisteAntwort(art, Version, []));
        }
        public Task<TextlisteAntwort> SetzeReihenfolge(Textlistenart art, TextlisteReihenfolgeAnfrage anfrage, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
