using AndyTours.App.Funktionen;
using AndyTours.Import.Overlays;
using AndyTours.App.ViewModels;
using AndyTours.Application.Overlays;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Tests.Datenordner;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiOverlaysViewModelTests
{
    [Fact]
    public async Task Real_store_drives_selection_color_toggle_and_context_lifetime()
    {
        using var temp = new TempOrdner();
        var store = new SqliteOverlaySpeicher(Path.Combine(temp.Pfad, "overlay.db"));
        using var context = new ApiOverlayKontext(store);
        var baseline = await store.Laden(default);
        var folderResult = await store.OrdnerAnlegen(new(baseline.Version, Guid.NewGuid(), null, "Trips"), default);
        var folder = folderResult.Id;
        var current = folderResult.Bestand;
        var importer = new OverlayGpxImportDienst(store);
        async Task<long> Add(string name, Guid requestId)
        {
            var request = new OverlayGpxImportAnfrage(current.Version, requestId, folder, name + ".gpx");
            var prepared = new OverlayGpxVorbereitung(request, new string('A', 64), new IReadOnlyList<Geoposition>[] { new[] { new Geoposition(48, 11) } });
            var response = await store.Importieren(prepared, default);
            current = response.Bestand;
            return response.Id;
        }
        long firstId = await Add("First", Guid.Parse("11111111-1111-1111-1111-111111111111"));
        long secondId = await Add("Second", Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var vm = new ApiOverlaysViewModel(context, store, importer, _ => true);
        await context.LadenAsync(default);
        Assert.True(vm.KannAendern);
        Assert.Null(vm.Ausgewaehlt);

        var rootFolder = Assert.Single(vm.Wurzeln);
        var toggle = rootFolder.IstEingeschaltet;
        rootFolder.IstEingeschaltet = toggle == true ? false : true;
        Assert.Equal(toggle, rootFolder.IstEingeschaltet);
        await vm.UmschaltenCommand.ExecutionTask!;
        Assert.NotEqual(toggle, rootFolder.IstEingeschaltet);
        var nodes = rootFolder.Kinder.ToArray();
        var first = nodes.Single(node => node.Id == firstId);
        var second = nodes.Single(node => node.Id == secondId);
        vm.Ausgewaehlt = first;
        vm.Bearbeitungsname = "Unsaved edit";
        var blue = vm.Farbauswahl.Single(option => option.Farbe == OverlayFarbe.Blau);
        vm.GewaehlteFarbe = blue;
        await vm.EigeneFarbeAendernCommand.ExecutionTask!;
        Assert.Equal("Unsaved edit", vm.Bearbeitungsname);
        Assert.Same(first, vm.Ausgewaehlt);
        Assert.Equal(OverlayFarbe.Blau, (await store.Laden(default)).Overlays.Single(item => item.Id == firstId).Farbe);

        vm.Ausgewaehlt = second;
        vm.GewaehlteFarbe = blue;
        await vm.EigeneFarbeAendernCommand.ExecutionTask!;
        Assert.Equal(OverlayFarbe.Blau, (await store.Laden(default)).Overlays.Single(item => item.Id == secondId).Farbe);
        vm.Dispose();
        Assert.NotNull(context.Anzeige);
        Assert.False(vm.LadenCommand.CanExecute(null));
    }

}
