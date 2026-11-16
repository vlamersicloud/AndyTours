using AndyTours.App.Funktionen;
using AndyTours.App.ViewModels;
using AndyTours.Application.Overlays;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiOverlayGpxCancelTests
{
    [Fact]
    public async Task Cancellation_after_success_keeps_receipt_confirmed_and_stops_remaining_queue()
    {
        string dir = Path.Combine(Path.GetTempPath(), "overlay-cancel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var reader = new Reader();
            var importer = new Importer();
            using var context = new ApiOverlayKontext(reader);
            using var vm = new ApiOverlaysViewModel(context, new Verwaltung(), importer, _ => true);
            await context.LadenAsync(default);
            string first = Path.Combine(dir, "first.gpx"), second = Path.Combine(dir, "second.gpx");
            await File.WriteAllTextAsync(first, "body");
            await File.WriteAllTextAsync(second, "body");
            vm.GpxDateienHinzufuegen([first, second]);
            importer.AfterSuccess = () => vm.GpxImportierenCommand.Cancel();
            await vm.GpxImportierenCommand.ExecuteAsync(null);
            Assert.Equal(1, importer.Calls);
            Assert.True(vm.Importdateien[0].Fertig);
            Assert.False(vm.Importdateien[0].Unklar);
            Assert.False(vm.Importdateien[1].Fertig);
            Assert.True(vm.GpxWartetAufReload);
            Assert.False(vm.GpxImportierenCommand.CanExecute(null));
        }
        finally { Directory.Delete(dir, true); }
    }

    private sealed class Reader : IOverlayLeser
    {
        int _version;
        public Task<OverlayBestandAntwort> Laden(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _version++;
            return Task.FromResult(new OverlayBestandAntwort(_version.ToString("X64", System.Globalization.CultureInfo.InvariantCulture), OverlayFarbe.Violett, [], []));
        }
        public Task<OverlayInhaltAntwort> Inhalt(long id, string version, CancellationToken ct) => throw new InvalidOperationException();
    }
    private sealed class Importer : IOverlayGpxImport
    {
        public int Calls { get; private set; }
        public Action? AfterSuccess { get; set; }
        public Task<OverlayAnlegenAntwort> Importieren(OverlayGpxImportAnfrage request, Stream stream, CancellationToken ct)
        {
            Calls++;
            var result = new OverlayAnlegenAntwort(new OverlayBestandAntwort(request.Version, OverlayFarbe.Violett, [], []), Calls);
            AfterSuccess?.Invoke();
            return Task.FromResult(result);
        }
    }
    private sealed class Verwaltung : IOverlayVerwaltung
    {
        public Task<OverlayAnlegenAntwort> OrdnerAnlegen(OverlayOrdnerAnlegenAnfrage a, CancellationToken c) => throw new NotImplementedException();
        public Task<OverlayBestandAntwort> OrdnerAendern(long id, OverlayOrdnerAendernAnfrage a, CancellationToken c) => throw new NotImplementedException();
        public Task<OverlayBestandAntwort> OrdnerLoeschen(long id, string v, CancellationToken c) => throw new NotImplementedException();
        public Task<OverlayBestandAntwort> OverlayAendern(long id, OverlayAendernAnfrage a, CancellationToken c) => throw new NotImplementedException();
        public Task<OverlayBestandAntwort> OverlayLoeschen(long id, string v, CancellationToken c) => throw new NotImplementedException();
        public Task<OverlayBestandAntwort> Sichtbarkeit(OverlaySichtbarkeitAnfrage a, CancellationToken c) => throw new NotImplementedException();
        public Task<OverlayBestandAntwort> GemeinsameFarbe(OverlayGemeinsameFarbeAnfrage a, CancellationToken c) => throw new NotImplementedException();
    }
}
