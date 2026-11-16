using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Tests.Datenordner;
using Microsoft.Data.Sqlite;
using CoreColor = AndyTours.Core.Modell.Overlayfarbe;
using ApiColor = AndyTours.Contracts.V1.OverlayFarbe;

namespace AndyTours.Tests.Daten;

public sealed class SqliteOverlayVerwaltungTests
{
    [Fact]
    public async Task FolderCreationIsIdempotentAndMutationsValidateCasAndCycles()
    {
        using var temp = new TempOrdner();
        string dbPath = Path.Combine(temp.Pfad, "overlay.db");
        var firstStore = new SqliteOverlaySpeicher(dbPath);
        var secondStore = new SqliteOverlaySpeicher(dbPath);
        var baseline = await firstStore.Laden(CancellationToken.None);
        Guid requestId = Guid.NewGuid();
        var create = new OverlayOrdnerAnlegenAnfrage(baseline.Version, requestId, null, "Alpen");
        var created = await firstStore.OrdnerAnlegen(create, CancellationToken.None);
        var replay = await secondStore.OrdnerAnlegen(create, CancellationToken.None);
        Assert.Equal(created.Id, replay.Id);
        Assert.Equal(created.Bestand.Version, replay.Bestand.Version);
        Assert.Equal(1, await CountFolders(dbPath));
        Assert.Single(replay.Bestand.Ordner);

        var changedReplay = await Assert.ThrowsAsync<StammdatenException>(() =>
            secondStore.OrdnerAnlegen(create with { Name = "Anders" }, CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.Konflikt, changedReplay.Art);

        Guid childRequest = Guid.NewGuid();
        var child = await firstStore.OrdnerAnlegen(
            new OverlayOrdnerAnlegenAnfrage(created.Bestand.Version, childRequest, created.Id, "Tal"),
            CancellationToken.None);
        var cycle = await Assert.ThrowsAsync<StammdatenException>(() =>
            firstStore.OrdnerAendern(created.Id,
                new OverlayOrdnerAendernAnfrage(child.Bestand.Version, child.Id, "Alpen"), CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.Ungueltig, cycle.Art);
        Assert.Equal(child.Bestand.Version, (await firstStore.Laden(CancellationToken.None)).Version);

        Guid duplicateRequest = Guid.NewGuid();
        var duplicate = await Assert.ThrowsAsync<StammdatenException>(() =>
            firstStore.OrdnerAnlegen(new OverlayOrdnerAnlegenAnfrage(child.Bestand.Version, duplicateRequest, null, "  alpen  "),
                CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.Ungueltig, duplicate.Art);

        var stale = await Assert.ThrowsAsync<StammdatenException>(() =>
            firstStore.OrdnerAendern(created.Id,
                new OverlayOrdnerAendernAnfrage(baseline.Version, null, "Neu"), CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.Konflikt, stale.Art);
        Assert.Equal(child.Bestand.Version, (await firstStore.Laden(CancellationToken.None)).Version);

        var deleted = await firstStore.OrdnerLoeschen(created.Id, child.Bestand.Version, CancellationToken.None);
        Assert.Empty(deleted.Ordner);
        Assert.Empty(deleted.Overlays);
        var replayAfterDelete = await secondStore.OrdnerAnlegen(create, CancellationToken.None);
        Assert.Equal(created.Id, replayAfterDelete.Id);
        Assert.Equal(created.Bestand.Version, replayAfterDelete.Bestand.Version);
        Assert.Empty((await secondStore.Laden(CancellationToken.None)).Ordner);
        Assert.Equal(0, await CountFolders(dbPath));
        Assert.Equal(created.Id, JsonSerializer.Deserialize<OverlayAnlegenAntwort>(
            JsonSerializer.Serialize(replayAfterDelete))!.Id);
    }

    [Fact]
    public async Task OverlayStateMutationsAndCascadesRollbackAtomically()
    {
        using var temp = new TempOrdner();
        string dbPath = Path.Combine(temp.Pfad, "state.db");
        long parent;
        long child;
        long firstOverlay;
        long secondOverlay;
        using (var db = OeffneDatenbank.Ausfuehren(dbPath))
        {
            parent = OverlayordnerTabelle.Einfuegen(db, null, "Parent");
            child = OverlayordnerTabelle.Einfuegen(db, parent, "Child");
            firstOverlay = AddOverlay(db, child, "First", [new Geoposition(10, 20)]);
            secondOverlay = AddOverlay(db, child, "Second", [new Geoposition(30, 40)]);
            OverlayfarbeEinstellung.Speichern(db, CoreColor.Violett);
            Exec(db, "INSERT INTO Einstellung (Schluessel,Wert) VALUES ('unrelated','keep');");
        }

        var store = new SqliteOverlaySpeicher(dbPath);
        var baseline = await store.Laden(CancellationToken.None);
        var beforeItems = baseline.Overlays.ToDictionary(x => x.Id);
        var renamed = await store.OverlayAendern(firstOverlay,
            new OverlayAendernAnfrage(baseline.Version, parent, "Renamed", ApiColor.Tuerkis), CancellationToken.None);
        var changed = renamed.Overlays.Single(x => x.Id == firstOverlay);
        Assert.Equal("Renamed", changed.Name);
        Assert.Equal(parent, changed.OrdnerId);
        Assert.Equal(ApiColor.Tuerkis, changed.Farbe);
        Assert.Equal(beforeItems[firstOverlay].IstEingeschaltet, changed.IstEingeschaltet);
        Assert.Equal(beforeItems[secondOverlay].Version, renamed.Overlays.Single(x => x.Id == secondOverlay).Version);
        var content = await store.Inhalt(firstOverlay, changed.Version, CancellationToken.None);
        Assert.Equal(new OverlayPosition(10, 20), content.Abschnitte.Single().Single());

        var recolored = await store.GemeinsameFarbe(new OverlayGemeinsameFarbeAnfrage(renamed.Version, ApiColor.Gelb), CancellationToken.None);
        Assert.Equal(ApiColor.Gelb, recolored.GemeinsameFarbe);
        Assert.Equal(changed.Version, recolored.Overlays.Single(x => x.Id == firstOverlay).Version);
        Assert.Equal("keep", ReadSetting(dbPath, "unrelated"));

        var mixed = await Assert.ThrowsAsync<StammdatenException>(() =>
            store.Sichtbarkeit(new OverlaySichtbarkeitAnfrage(recolored.Version, [firstOverlay, 999999], false), CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.NichtGefunden, mixed.Art);
        Assert.Equal(recolored.Version, (await store.Laden(CancellationToken.None)).Version);

        using (var db = OeffneDatenbank.Ausfuehren(dbPath))
            Exec(db, $"CREATE TRIGGER abort_second_visibility BEFORE UPDATE OF Eingeschaltet ON Overlay WHEN OLD.Id={secondOverlay} BEGIN SELECT RAISE(ABORT,'injected'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => store.Sichtbarkeit(
            new OverlaySichtbarkeitAnfrage(recolored.Version, [firstOverlay, secondOverlay], false), CancellationToken.None));
        Assert.Equal(recolored.Version, (await store.Laden(CancellationToken.None)).Version);
        Assert.True((await store.Laden(CancellationToken.None)).Overlays.All(x => x.IstEingeschaltet));

        using (var db = OeffneDatenbank.Ausfuehren(dbPath))
            Exec(db, "DROP TRIGGER abort_second_visibility;");
        var visible = await store.Sichtbarkeit(
            new OverlaySichtbarkeitAnfrage(recolored.Version, [firstOverlay, secondOverlay], false), CancellationToken.None);
        Assert.All(visible.Overlays, item => Assert.False(item.IstEingeschaltet));

        using (var db = OeffneDatenbank.Ausfuehren(dbPath))
            Exec(db, $"CREATE TRIGGER abort_folder_cascade BEFORE DELETE ON Overlay WHEN OLD.Id={firstOverlay} BEGIN SELECT RAISE(ABORT,'injected'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => store.OrdnerLoeschen(parent, visible.Version, CancellationToken.None));
        Assert.Equal(visible.Version, (await store.Laden(CancellationToken.None)).Version);
        Assert.Equal(2, (await store.Laden(CancellationToken.None)).Overlays.Count);
        Assert.Equal("keep", ReadSetting(dbPath, "unrelated"));

        var canceled = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.OrdnerLoeschen(parent, visible.Version, canceled));
        Assert.Equal(visible.Version, (await store.Laden(CancellationToken.None)).Version);
        using (var db = OeffneDatenbank.Ausfuehren(dbPath))
            Exec(db, "DROP TRIGGER abort_folder_cascade;");
        var cascade = await store.OrdnerLoeschen(parent, visible.Version, CancellationToken.None);
        Assert.Empty(cascade.Ordner);
        Assert.Empty(cascade.Overlays);
        Assert.Equal("keep", ReadSetting(dbPath, "unrelated"));
    }

    private static long AddOverlay(SqliteConnection db, long folder, string name, IReadOnlyList<Geoposition> points) =>
        OverlaysTabelle.Einfuegen(db, new Overlay(0, folder, name, null, true, new[] { points }));

    private static string? ReadSetting(string path, string key)
    {
        using var db = OeffneDatenbank.Ausfuehren(path);
        using var command = db.CreateCommand();
        command.CommandText = "SELECT Wert FROM Einstellung WHERE Schluessel=$key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    private static void Exec(SqliteConnection db, string sql)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static async Task<long> CountFolders(string path)
    {
        using var db = OeffneDatenbank.Ausfuehren(path);
        using var command = db.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Overlayordner;";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
