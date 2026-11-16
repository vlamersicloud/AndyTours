using System.Buffers.Binary;
using System.Globalization;
using AndyTours.Application.Stammdaten;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Tests.Datenordner;
using Microsoft.Data.Sqlite;
using ApiFarbe = AndyTours.Contracts.V1.OverlayFarbe;
using ApiPosition = AndyTours.Contracts.V1.OverlayPosition;
using CoreFarbe = AndyTours.Core.Modell.Overlayfarbe;

namespace AndyTours.Tests.Daten;

public sealed class SqliteOverlaySpeicherTests
{
    [Fact]
    public async Task BestandAndInhaltPreserveGeometryAndScopeVersions()
    {
        using var temp = new TempOrdner();
        string dbPath = Path.Combine(temp.Pfad, "overlay.db");
        using (var db = OeffneDatenbank.Ausfuehren(dbPath))
        {
            long folder = OverlayordnerTabelle.Einfuegen(db, null, "Folder");
            long child = OverlayordnerTabelle.Einfuegen(db, folder, "Child");
            var sections = new IReadOnlyList<Geoposition>[]
            {
                new[] { new Geoposition(48.1, 11.5), new Geoposition(48.2, 11.6) },
                new[] { new Geoposition(49.0, 12.0) }
            };
            long createdId = OverlaysTabelle.Einfuegen(db, new Overlay(0, child, "Track", CoreFarbe.Tuerkis, true, sections));
            Exec(db, $"INSERT INTO Einstellung (Schluessel,Wert) VALUES ('test.overlay.id','{createdId}');");
            OverlayfarbeEinstellung.Speichern(db, CoreFarbe.Gelb);
            Exec(db, "INSERT INTO Einstellung (Schluessel,Wert) VALUES ('unrelated-secret','secret-value');");
        }
        long id;
        using (var db = OeffneDatenbank.Ausfuehren(dbPath))
        {
            using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT Wert FROM Einstellung WHERE Schluessel='test.overlay.id';";
            id = long.Parse((string)cmd.ExecuteScalar()!, CultureInfo.InvariantCulture);
            Exec(db, "DELETE FROM Einstellung WHERE Schluessel='test.overlay.id';");
        }
        var service = new SqliteOverlaySpeicher(dbPath);
        var first = await service.Laden(CancellationToken.None);
        var repeated = await service.Laden(CancellationToken.None);
        Assert.Equal(first.Version, repeated.Version);
        var meta = Assert.Single(first.Overlays);
        Assert.Equal(ApiFarbe.Tuerkis, meta.Farbe);
        Assert.True(meta.IstEingeschaltet);
        Assert.Equal(ApiFarbe.Gelb, first.GemeinsameFarbe);
        Assert.Equal(2, first.Ordner.Count);
        Assert.Equal(first.Ordner[1].Id, meta.OrdnerId);
        var publicJson = System.Text.Json.JsonSerializer.Serialize(new { first, content = await service.Inhalt(id, meta.Version, CancellationToken.None) });
        Assert.DoesNotContain("secret-value", publicJson, StringComparison.Ordinal);

        var content = await service.Inhalt(id, meta.Version, CancellationToken.None);
        Assert.Equal(meta.Version, content.Version);
        Assert.Equal(2, content.Abschnitte.Count);
        Assert.Equal(2, content.Abschnitte[0].Count);
        Assert.Single(content.Abschnitte[1]);
        Assert.Equal(new ApiPosition(48.2, 11.6), content.Abschnitte[0][1]);

        using (var db = OeffneDatenbank.Ausfuehren(dbPath))
            OverlayordnerTabelle.Umbenennen(db, first.Ordner[0].Id, "Renamed");
        var folderRenamed = await service.Laden(CancellationToken.None);
        Assert.NotEqual(first.Version, folderRenamed.Version);
        Assert.Equal(meta.Version, Assert.Single(folderRenamed.Overlays).Version);

        using (var db = OeffneDatenbank.Ausfuehren(dbPath))
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE Overlay SET Punkte=$points WHERE Id=$id;";
            cmd.Parameters.AddWithValue("$points", OverlayPunkte.Verpacken(new IReadOnlyList<Geoposition>[] { new[] { new Geoposition(47.0, 10.0) } }));
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        var geometryChanged = await service.Laden(CancellationToken.None);
        var geometryMeta = Assert.Single(geometryChanged.Overlays);
        Assert.NotEqual(meta.Version, geometryMeta.Version);
        var geometryConflict = await Assert.ThrowsAsync<StammdatenException>(() => service.Inhalt(id, meta.Version, CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.Konflikt, geometryConflict.Art);

        using (var db = OeffneDatenbank.Ausfuehren(dbPath))
            OverlaysTabelle.Umbenennen(db, id, "Renamed Track");
        var renamed = await service.Laden(CancellationToken.None);
        var changedMeta = Assert.Single(renamed.Overlays);
        Assert.NotEqual(meta.Version, changedMeta.Version);
        var conflict = await Assert.ThrowsAsync<StammdatenException>(() => service.Inhalt(id, meta.Version, CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.Konflikt, conflict.Art);

        using (var db = OeffneDatenbank.Ausfuehren(dbPath))
            Exec(db, "UPDATE Einstellung SET Wert='secret-changed' WHERE Schluessel='unrelated-secret';");
        var afterSetting = await service.Laden(CancellationToken.None);
        Assert.Equal(renamed.Version, afterSetting.Version);
        Assert.Equal(changedMeta.Version, Assert.Single(afterSetting.Overlays).Version);
        var serialized = System.Text.Json.JsonSerializer.Serialize(new { afterSetting, changedMeta });
        Assert.DoesNotContain("secret-changed", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidatesInputsAndRejectsCorruptCountsWithoutGeometryAllocation()
    {
        using var temp = new TempOrdner();
        string dbPath = Path.Combine(temp.Pfad, "overlay.db");
        using (var db = OeffneDatenbank.Ausfuehren(dbPath))
        {
            long id = OverlayordnerTabelle.Einfuegen(db, null, "Folder");
            long overlayId = OverlaysTabelle.Einfuegen(db, new Overlay(0, id, "Bad", null, false,
                new IReadOnlyList<Geoposition>[] { new[] { new Geoposition(1, 2) } }));
            Assert.Throws<ArgumentException>(() => new SqliteOverlaySpeicher("relative.db"));
            var service = new SqliteOverlaySpeicher(dbPath);
            var notFound = await Assert.ThrowsAsync<StammdatenException>(() => service.Inhalt(999999, new string('A', 64), CancellationToken.None));
            Assert.Equal(StammdatenFehlerart.NichtGefunden, notFound.Art);
            var badId = await Assert.ThrowsAsync<StammdatenException>(() => service.Inhalt(0, new string('A', 64), CancellationToken.None));
            Assert.Equal(StammdatenFehlerart.Ungueltig, badId.Art);
            var badToken = await Assert.ThrowsAsync<StammdatenException>(() => service.Inhalt(overlayId, "bad", CancellationToken.None));
            Assert.Equal(StammdatenFehlerart.Ungueltig, badToken.Art);
            var preCanceled = new CancellationToken(true);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.Laden(preCanceled));

            byte[] corrupt = new byte[9];
            corrupt[0] = 1;
            BinaryPrimitives.WriteInt32LittleEndian(corrupt.AsSpan(1, 4), int.MaxValue);
            BinaryPrimitives.WriteInt32LittleEndian(corrupt.AsSpan(5, 4), int.MaxValue);
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "UPDATE Overlay SET Punkte=$blob WHERE Id=$id;";
                cmd.Parameters.AddWithValue("$blob", corrupt);
                cmd.Parameters.AddWithValue("$id", overlayId);
                cmd.ExecuteNonQuery();
            }
        }
        var reader = new SqliteOverlaySpeicher(dbPath);
        var metadata = await reader.Laden(CancellationToken.None);
        Assert.Single(metadata.Overlays);
        var error = await Assert.ThrowsAsync<StammdatenException>(() => reader.Inhalt(metadata.Overlays[0].Id, metadata.Overlays[0].Version, CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.Ungueltig, error.Art);
    }

    private static void Exec(SqliteConnection db, string sql)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
