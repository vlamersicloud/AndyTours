using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using AndyTours.Tests.Datenordner;
using Microsoft.Data.Sqlite;

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

    private static async Task<long> CountFolders(string path)
    {
        using var db = OeffneDatenbank.Ausfuehren(path);
        using var command = db.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Overlayordner;";
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
