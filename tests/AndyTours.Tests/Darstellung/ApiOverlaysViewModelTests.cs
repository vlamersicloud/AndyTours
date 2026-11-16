using AndyTours.App.Funktionen;
using AndyTours.Import.Overlays;
using AndyTours.App.ViewModels;
using AndyTours.Application.Overlays;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Tests.Datenordner;
using System.ComponentModel;

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
        using var vm = new ApiOverlaysViewModel(context, store, importer, _ => true);
        await context.LadenAsync(default);
        Assert.True(vm.KannAendern);
        Assert.Null(vm.Ausgewaehlt);
        Assert.True(vm.OrdnerAnlegenCommand.CanExecute(null));

        var sharedBlue = vm.GemeinsameFarbauswahl.Single(option => option.Farbe == OverlayFarbe.Blau);
        vm.GewaehlteGemeinsameFarbe = sharedBlue;
        await vm.GemeinsameFarbeAendernCommand.ExecutionTask!;
        Assert.Equal(OverlayFarbe.Blau, (await store.Laden(default)).GemeinsameFarbe);

        var rootFolder = Assert.Single(vm.Wurzeln);
        var toggle = rootFolder.IstEingeschaltet;
        rootFolder.IstEingeschaltet = toggle == true ? false : true;
        await vm.UmschaltenCommand.ExecutionTask!;
        Assert.NotEqual(toggle, rootFolder.IstEingeschaltet);

        var node = new ApiOverlayknoten(new OverlayMetadatenAntwort(999, null, "probe", null, true, new string('A', 64)), "#000000", (_, _) => { });
        bool notified = false;
        node.PropertyChanged += (_, e) => notified |= e.PropertyName == nameof(ApiOverlayknoten.IstEingeschaltet);
        node.IstEingeschaltet = false;
        Assert.True(notified);
        Assert.True(node.IstEingeschaltet);

        var nodes = rootFolder.Kinder.ToArray();
        var first = nodes.Single(item => item.Id == firstId);
        var second = nodes.Single(item => item.Id == secondId);
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

    [Fact]
    public async Task Real_store_imports_queued_files_with_fresh_per_file_versions()
    {
        using var temp = new TempOrdner();
        var store = new SqliteOverlaySpeicher(Path.Combine(temp.Pfad, "queue.db"));
        using var context = new ApiOverlayKontext(store);
        var snapshot = await store.Laden(default);
        var a = await store.OrdnerAnlegen(new(snapshot.Version, Guid.NewGuid(), null, "A"), default);
        var b = await store.OrdnerAnlegen(new(a.Bestand.Version, Guid.NewGuid(), null, "B"), default);
        var importer = new OverlayGpxImportDienst(store);
        using var vm = new ApiOverlaysViewModel(context, store, importer, _ => true);
        await context.LadenAsync(default);
        var folders = vm.Wurzeln.OrderBy(n => n.Name).ToArray();
        var folderA = folders.Single(n => n.Name == "A");
        var folderB = folders.Single(n => n.Name == "B");
        vm.Ausgewaehlt = folderA;

        string gpx = "<?xml version=\"1.0\"?><gpx version=\"1.1\" creator=\"test\" xmlns=\"http://www.topografix.com/GPX/1/1\"><trk><name>sample</name><trkseg><trkpt lat=\"48.1\" lon=\"11.5\"><ele>520</ele></trkpt><trkpt lat=\"48.2\" lon=\"11.6\" /></trkseg></trk></gpx>";
        string one = Path.Combine(temp.Pfad, "one.gpx");
        string two = Path.Combine(temp.Pfad, "two.gpx");
        await File.WriteAllTextAsync(one, gpx);
        await File.WriteAllTextAsync(two, gpx);
        vm.GpxDateienHinzufuegen([one, two]);
        Assert.Equal(2, vm.Importdateien.Count);
        Assert.All(vm.Importdateien, row => Assert.Null(row.Anfrage));
        vm.Ausgewaehlt = folderB;

        await vm.GpxImportierenCommand.ExecuteAsync(null);

        Assert.All(vm.Importdateien, row => Assert.True(row.Fertig));
        Assert.All(vm.Importdateien, row => Assert.Contains("bestätigt", row.Status, StringComparison.OrdinalIgnoreCase));
        var final = await store.Laden(default);
        Assert.Equal(2, final.Overlays.Count);
        Assert.All(final.Overlays, item => Assert.Equal(a.Id, item.OrdnerId));
        Assert.All(vm.Importdateien, row => Assert.NotNull(row.Anfrage));
        Assert.Equal(2, vm.Importdateien.Select(row => row.Anfrage!.AnfrageId).Distinct().Count());
        Assert.Equal(2, vm.Importdateien.Select(row => row.Anfrage!.Version).Distinct().Count());

        var priorId = vm.Importdateien[0].Anfrage!.AnfrageId;
        vm.GpxDateienHinzufuegen([one]);
        Assert.Equal(3, vm.Importdateien.Count);
        Assert.NotEqual(priorId, vm.Importdateien[2].AnfrageId);
        Assert.Null(vm.Importdateien[2].Anfrage);
        vm.Dispose();
        Assert.True(File.Exists(one));
        Assert.True(File.Exists(two));
        Assert.NotNull(context.Anzeige);
    }
}
