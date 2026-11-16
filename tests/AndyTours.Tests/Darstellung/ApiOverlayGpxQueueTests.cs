using AndyTours.App.Funktionen;
using AndyTours.App.ViewModels;
using AndyTours.Application.Overlays;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiOverlayGpxQueueTests
{
    [Fact]
    public async Task Confirmed_import_survives_failed_reload_and_missing_file_is_rejected_before_send()
    {
        string temp = Path.Combine(Path.GetTempPath(), "overlay-gpx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var reader = new SequencedReader();
            var importer = new ReceiptImporter();
            using var context = new ApiOverlayKontext(reader);
            using var vm = new ApiOverlaysViewModel(context, new NoopVerwaltung(), importer, _ => true);
            await context.LadenAsync(default);
            var missing = Path.Combine(temp, "missing.gpx");
            vm.GpxDateienHinzufuegen([missing]);
            await vm.GpxImportierenCommand.ExecuteAsync(null);
            Assert.True(vm.Importdateien[0].Fertig);
            Assert.False(vm.Importdateien[0].Unklar);
            Assert.Equal(0, importer.Calls);
            var valid = Path.Combine(temp, "valid.gpx");
            await File.WriteAllTextAsync(valid, "gpx");
            vm.GpxDateienHinzufuegen([valid]);
            reader.FailNextMetadata = true;
            await vm.GpxImportierenCommand.ExecuteAsync(null);
            Assert.Equal(1, importer.Calls);
            Assert.True(vm.Importdateien[1].Fertig);
            Assert.False(vm.Importdateien[1].Unklar);
            Assert.True(vm.GpxWartetAufReload);
            Assert.False(vm.GpxImportierenCommand.CanExecute(null));
            reader.FailNextMetadata = false;
            await vm.LadenCommand.ExecuteAsync(null);
            Assert.False(vm.GpxWartetAufReload);
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    private sealed class SequencedReader : IOverlayLeser
    {
        public bool FailNextMetadata { get; set; }
        public Task<OverlayBestandAntwort> Laden(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailNextMetadata) throw new IOException();
            return Task.FromResult(new OverlayBestandAntwort(new string('A', 64), OverlayFarbe.Violett, [], []));
        }
        public Task<OverlayInhaltAntwort> Inhalt(long overlayId, string erwarteteVersion, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }

    private sealed class ReceiptImporter : IOverlayGpxImport
    {
        public int Calls { get; private set; }
        public Task<OverlayAnlegenAntwort> Importieren(OverlayGpxImportAnfrage anfrage, Stream inhalt, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new OverlayAnlegenAntwort(new OverlayBestandAntwort(new string('B', 64), OverlayFarbe.Violett, [],
                [new OverlayMetadatenAntwort(7, null, "valid", null, false, new string('C', 64))]), 7));
        }
    }

    private sealed class NoopVerwaltung : IOverlayVerwaltung
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
