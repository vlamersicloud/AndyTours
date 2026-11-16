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
        await first.LadenAsync(default);
        await second.LadenAsync(default);
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
        var position = new OverlayPosition(10, 20);
        var sections = new List<IReadOnlyList<OverlayPosition>> { new[] { position }, new[] { new OverlayPosition(30, 40) } };
        var items = new List<OverlayMetadatenAntwort> { meta };
        var folders = new List<OverlayOrdnerAntwort> { new(8, null, "Folder") };
        var reader = new FakeReader(new OverlayBestandAntwort(VersionA, OverlayFarbe.Grau, folders, items))
        {
            Content = new OverlayInhaltAntwort(3, VersionA, sections)
        };
        using var context = new ApiOverlayKontext(reader);
        await context.LadenAsync(default);
        items[0] = meta with { Name = "Mutated" };
        folders[0] = new OverlayOrdnerAntwort(8, null, "Mutated");
        sections[0] = new[] { new OverlayPosition(0, 0) };
        Assert.Equal("Copied", Assert.Single(context.Anzeige!.Bestand.Overlays).Name);
        Assert.Equal("Folder", Assert.Single(context.Anzeige.Bestand.Ordner).Name);
        var copied = Assert.Single(context.Anzeige.Eingeschaltete);
        Assert.Equal(2, copied.Abschnitte.Count);
        Assert.Equal(10, copied.Abschnitte[0][0].Breite);
        Assert.Equal(20, copied.Abschnitte[0][0].Laenge);
        Assert.Single(copied.Abschnitte[0]);

        Assert.Throws<InvalidOperationException>(() => ApiOverlayMapper.AlsOverlay(meta,
            new OverlayInhaltAntwort(3, VersionB, sections), default));
        Assert.Throws<InvalidOperationException>(() => ApiOverlayMapper.AlsOverlay(meta,
            new OverlayInhaltAntwort(3, VersionA, new IReadOnlyList<OverlayPosition>[] { new[] { new OverlayPosition(double.NaN, 0) } }), default));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => ApiOverlayMapper.AlsOverlay(meta,
            new OverlayInhaltAntwort(3, VersionA, sections), canceled.Token));
    }

    private static OverlayBestandAntwort Snapshot(string name, OverlayFarbe color, long id, string version) =>
        new(version, color, Array.Empty<OverlayOrdnerAntwort>(),
            new[] { new OverlayMetadatenAntwort(id, null, name, null, true, version) });

    private sealed class FakeReader(OverlayBestandAntwort bestand) : IOverlayLeser
    {
        public OverlayBestandAntwort Bestand { get; set; } = bestand;
        public OverlayInhaltAntwort? Content { get; set; }
        public int ContentReads { get; private set; }
        public Task<OverlayBestandAntwort> Laden(CancellationToken cancellationToken) => Task.FromResult(Bestand);
        public Task<OverlayInhaltAntwort> Inhalt(long overlayId, string erwarteteVersion, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ContentReads++;
            return Task.FromResult(Content ?? new OverlayInhaltAntwort(overlayId, erwarteteVersion,
                new IReadOnlyList<OverlayPosition>[] { new[] { new OverlayPosition(1, 2) } }));
        }
    }
}
