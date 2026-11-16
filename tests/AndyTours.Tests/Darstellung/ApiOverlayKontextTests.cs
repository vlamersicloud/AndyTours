using AndyTours.Application.Overlays;
using AndyTours.App.Funktionen;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiOverlayKontextTests
{
    private const string VersionA = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string VersionB = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

    [Fact]
    public async Task Contexts_are_isolated_and_reuse_only_matching_item_versions()
    {
        var one = new FakeReader(Snapshot("RootOne", OverlayFarbe.Rot, 1, VersionA));
        var two = new FakeReader(Snapshot("RootTwo", OverlayFarbe.Blau, 9, VersionA));
        using var first = new ApiOverlayKontext(one);
        using var second = new ApiOverlayKontext(two);
        await first.LadenAsync(default); await second.LadenAsync(default);
        Assert.Equal("RootOne", Assert.Single(first.Anzeige!.Eingeschaltete).Name);
        Assert.Equal("RootTwo", Assert.Single(second.Anzeige!.Eingeschaltete).Name);
        Assert.Equal(Overlayfarbe.Rot, first.Anzeige.Farbe);
        Assert.Equal(Overlayfarbe.Blau, second.Anzeige.Farbe);
        await first.LadenAsync(default);
        Assert.Equal(1, one.ContentReads);
        one.Bestand = Snapshot("RootOneChanged", OverlayFarbe.Gruen, 1, VersionB);
        await first.LadenAsync(default);
        Assert.Equal(2, one.ContentReads);
        Assert.Equal("RootOneChanged", Assert.Single(first.Anzeige!.Eingeschaltete).Name);
        Assert.Equal(1, two.ContentReads);
        first.Dispose();
        Assert.Null(first.Anzeige);
        Assert.Empty(first.Anzeige?.Eingeschaltete ?? Array.Empty<Overlay>());
    }

    [Fact]
    public async Task Applied_snapshot_copies_lists_and_mapper_validates_versions_and_points()
    {
        var meta = new OverlayMetadatenAntwort(3, null, "Copied", null, true, VersionA);
        var sections = new List<IReadOnlyList<OverlayPosition>> { new[] { new OverlayPosition(10, 20) }, new[] { new OverlayPosition(30, 40) } };
        var items = new List<OverlayMetadatenAntwort> { meta };
        var folders = new List<OverlayOrdnerAntwort> { new(8, null, "Folder") };
        var reader = new FakeReader(new OverlayBestandAntwort(VersionA, OverlayFarbe.Grau, folders, items)) { Content = new OverlayInhaltAntwort(3, VersionA, sections) };
        using var context = new ApiOverlayKontext(reader);
        await context.LadenAsync(default);
        items[0] = meta with { Name = "Mutated" }; folders[0] = new OverlayOrdnerAntwort(8, null, "Mutated"); sections[0] = new[] { new OverlayPosition(0, 0) };
        Assert.Equal("Copied", Assert.Single(context.Anzeige!.Bestand.Overlays).Name);
        Assert.Equal("Folder", Assert.Single(context.Anzeige.Bestand.Ordner).Name);
        var copied = Assert.Single(context.Anzeige.Eingeschaltete);
        Assert.Equal(2, copied.Abschnitte.Count); Assert.Equal(10, copied.Abschnitte[0][0].Breite); Assert.Equal(20, copied.Abschnitte[0][0].Laenge);
        Assert.Single(copied.Abschnitte[0]);
        Assert.Throws<InvalidOperationException>(() => ApiOverlayMapper.AlsOverlay(meta, new OverlayInhaltAntwort(3, VersionB, sections), default));
        Assert.Throws<InvalidOperationException>(() => ApiOverlayMapper.AlsOverlay(meta, new OverlayInhaltAntwort(3, VersionA, new IReadOnlyList<OverlayPosition>[] { new[] { new OverlayPosition(double.NaN, 0) } }), default));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => ApiOverlayMapper.AlsOverlay(meta, new OverlayInhaltAntwort(3, VersionA, sections), canceled.Token));
    }

    [Fact]
    public async Task Newer_load_wins_and_cancellation_or_disposal_prevents_pending_apply()
    {
        var reader = new FakeReader(Snapshot("old", OverlayFarbe.Rot, 1, VersionA));
        using var context = new ApiOverlayKontext(reader);
        var changes = 0;
        context.Geaendert += (_, _) => changes++;
        var oldGate = new TaskCompletionSource<OverlayBestandAntwort>(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.DelayedLoad = oldGate.Task;
        var oldLoad = context.LadenAsync(default);
        reader.DelayedLoad = null;
        reader.Bestand = Snapshot("new", OverlayFarbe.Blau, 2, VersionB);
        await context.LadenAsync(default);
        int afterNew = changes;
        oldGate.SetResult(Snapshot("old", OverlayFarbe.Rot, 1, VersionA));
        await oldLoad;
        Assert.Equal("new", Assert.Single(context.Anzeige!.Bestand.Overlays).Name);
        Assert.Equal(afterNew, changes);

        var geometryGate = new TaskCompletionSource<OverlayInhaltAntwort>(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.Bestand = Snapshot("cancelled", OverlayFarbe.Gruen, 3, VersionA);
        reader.DelayedContent = geometryGate.Task;
        using var cts = new CancellationTokenSource();
        var canceledLoad = context.LadenAsync(cts.Token);
        cts.Cancel();
        geometryGate.SetResult(Content(3, VersionA));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledLoad);
        Assert.Equal("new", Assert.Single(context.Anzeige!.Bestand.Overlays).Name);

        var disposalGate = new TaskCompletionSource<OverlayInhaltAntwort>(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.Bestand = Snapshot("disposed", OverlayFarbe.Gruen, 4, VersionB);
        reader.DelayedContent = disposalGate.Task;
        int beforeDispose = changes;
        var pending = context.LadenAsync(default);
        context.Dispose(); context.Dispose();
        disposalGate.SetResult(Content(4, VersionB));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Null(context.Anzeige);
        Assert.Equal(beforeDispose, changes);
    }

    private static OverlayInhaltAntwort Content(long id, string version) => new(id, version, new IReadOnlyList<OverlayPosition>[] { new[] { new OverlayPosition(1, 2) } });
    private static OverlayBestandAntwort Snapshot(string name, OverlayFarbe color, long id, string version) => new(version, color, Array.Empty<OverlayOrdnerAntwort>(), new[] { new OverlayMetadatenAntwort(id, null, name, null, true, version) });

    private sealed class FakeReader(OverlayBestandAntwort bestand) : IOverlayLeser
    {
        public OverlayBestandAntwort Bestand { get; set; } = bestand;
        public OverlayInhaltAntwort? Content { get; set; }
        public Task<OverlayBestandAntwort>? DelayedLoad { get; set; }
        public Task<OverlayInhaltAntwort>? DelayedContent { get; set; }
        public int ContentReads { get; private set; }
        public Task<OverlayBestandAntwort> Laden(CancellationToken cancellationToken) => DelayedLoad ?? Task.FromResult(Bestand);
        public Task<OverlayInhaltAntwort> Inhalt(long overlayId, string erwarteteVersion, CancellationToken cancellationToken)
        {
            ContentReads++;
            return DelayedContent ?? Task.FromResult(Content ?? ApiOverlayKontextTests.Content(overlayId, erwarteteVersion));
        }
    }
}
