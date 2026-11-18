using System.ComponentModel;
using System.Security.Cryptography;
using AndyTours.App.ViewModels;
using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Sicherung;

public sealed class ApiWiederherstellungsvorschauDownloadTests
{
    private static readonly byte[] Payload = [11, 22, 33, 44];
    private static readonly string Hash = Convert.ToHexString(SHA256.HashData(Payload));

    [Fact]
    public async Task DownloadIsExplicitAndSelectionChangeCancelsLateFakeResponse()
    {
        var (vm, client, first, second) = Create();
        var directory = NewDirectory();
        var target = Path.Combine(directory, "manual.db");
        Task<bool>? pending = null;
        try
        {
            await vm.LadenAsync();
            Assert.Equal(0, client.Calls);
            Assert.False(vm.KannDownloaden);
            vm.Auswahl = first;
            Assert.True(vm.KannDownloaden);
            Assert.Equal(0, client.Calls);

            pending = vm.DownloadNachAsync(target);
            var busyStatus = vm.DownloadStatus;
            await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            vm.Auswahl = second;
            client.Release.TrySetResult(true);
            Assert.False(await pending!.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(1, client.Calls);
            Assert.Equal(busyStatus, vm.DownloadStatus);
            Assert.False(File.Exists(target));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally
        {
            client.Release.TrySetResult(true);
            if (pending is not null) await Drain(pending);
            vm.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CloseSuppressesLateStatusAndFinalFile()
    {
        var (vm, client, selected, _) = Create();
        var directory = NewDirectory();
        var target = Path.Combine(directory, "closed.db");
        var changesAfterDispose = 0;
        Task<bool>? pending = null;
        try
        {
            await vm.LadenAsync();
            vm.Auswahl = selected;
            pending = vm.DownloadNachAsync(target);
            await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            vm.PropertyChanged += Count;
            vm.Dispose();
            changesAfterDispose = 0;
            client.Release.TrySetResult(true);
            await pending!.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, changesAfterDispose);
            Assert.False(File.Exists(target));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally
        {
            client.Release.TrySetResult(true);
            if (pending is not null) await Drain(pending);
            vm.Dispose();
            Directory.Delete(directory, recursive: true);
        }

        void Count(object? sender, PropertyChangedEventArgs args) => changesAfterDispose++;
    }

    [Fact]
    public async Task CallerCancellationDuringFakePortDoesNotPublish()
    {
        var (vm, client, selected, _) = Create();
        var directory = NewDirectory();
        var target = Path.Combine(directory, "cancelled.db");
        using var caller = new CancellationTokenSource();
        Task<bool>? pending = null;
        try
        {
            await vm.LadenAsync();
            vm.Auswahl = selected;
            pending = vm.DownloadNachAsync(target, caller.Token);
            await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            caller.Cancel();
            client.Release.TrySetResult(true);
            Assert.False(await pending!.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(File.Exists(target));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally
        {
            caller.Cancel();
            client.Release.TrySetResult(true);
            if (pending is not null) await Drain(pending);
            vm.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static (ApiWiederherstellungsvorschauViewModel Vm, FakeDownload Client,
        SicherungAntwort First, SicherungAntwort Second) Create()
    {
        var first = Entry(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var second = Entry(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var client = new FakeDownload();
        var catalog = new FakeCatalog(new SicherungListeAntwort([first, second]));
        var preview = new FakePreview();
        return (ApiWiederherstellungsvorschauViewModel.CreateWithDownloadClient(catalog, preview, client), client, first, second);
    }

    private static SicherungAntwort Entry(Guid id) =>
        new(id, Sicherungsart.LegacySqlite, null, Payload.LongLength, Hash)
        { LegacyZeitstempel = "2026-11-18_05-00-00" };

    private static string NewDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "AndyTours-vm-download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task Drain(Task<bool> task)
    {
        try { await task.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception) { }
    }

    private sealed class FakeCatalog(SicherungListeAntwort response) : ISicherungskatalog
    {
        public Task<SicherungListeAntwort> Liste(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response);
        }
    }

    private sealed class FakePreview : IWiederherstellungsvorschau
    {
        public Task<WiederherstellungsvorschauAntwort> Pruefen(
            WiederherstellungsvorschauAnfrage anfrage, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Preview is not part of download tests.");
    }

    private sealed class FakeDownload : ISicherungsdownloadClient
    {
        public int Calls { get; private set; }
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task Download(SicherungAntwort selected, Stream destination, CancellationToken cancellationToken)
        {
            Calls++;
            Started.TrySetResult(true);
            await Release.Task;
            await destination.WriteAsync(Payload, CancellationToken.None);
        }
    }
}
