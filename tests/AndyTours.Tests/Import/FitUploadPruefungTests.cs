using AndyTours.Application.Import;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using AndyTours.Import.Dateien;
using AndyTours.Tests.Garmin;
using Xunit;

namespace AndyTours.Tests.Import;

[Collection(FitTestdatei.Testgruppe)]
public sealed class FitUploadPruefungTests
{
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "FitUploadReview-" + Guid.NewGuid().ToString("N"));
        public string DataRoot => Path.Combine(Root, "data");
        public SqliteImportUploadSpeicher Store { get; }
        public FitUploadDienst Dienst { get; }
        public Fixture()
        {
            Directory.CreateDirectory(Root);
            var database = Path.Combine(Root, "db.sqlite");
            using (var db = OeffneDatenbank.Ausfuehren(database))
            using (var tx = db.BeginTransaction())
            {
                var activity = AktivitaetenTabelle.Einfuegen(db, tx, new AndyTours.Core.Modell.Aktivitaet { Name = "Rad", IstHaupt = true });
                EquipmentsTabelle.Einfuegen(db, tx, new AndyTours.Core.Modell.Equipment { Name = "Test-Rad", AktivitaetId = activity, IstStandard = true });
                tx.Commit();
            }
            Store = new SqliteImportUploadSpeicher(database);
            Dienst = new FitUploadDienst(Store, DataRoot, TimeZoneInfo.Utc);
        }
        public string Source(Guid id) => Path.Combine(DataRoot, "FitUploads", id.ToString("N"), "original.fit");
        public byte[] MakeFit(DateTime start)
        {
            var path = Path.Combine(Root, Guid.NewGuid().ToString("N") + ".fit");
            FitTestdatei.Schreibe(path, new FitTestdatei.Werte(start, VerschiebungStunden: 0));
            var bytes = File.ReadAllBytes(path);
            File.Delete(path);
            return bytes;
        }
        public async Task<FitUploadAntwort> Upload(Guid id, string name, byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            return await Dienst.Hochladen(id, name, stream, CancellationToken.None);
        }
        public void Dispose() { try { Directory.Delete(Root, true); } catch (IOException) { } }
    }

    [Fact]
    public async Task GueltigeDateiWirdTrotzUngueltigemGeschwisterGeprueftUndReceiptReplayt()
    {
        using var f = new Fixture();
        var validId = Guid.NewGuid();
        var invalidId = Guid.NewGuid();
        var good = await f.Upload(validId, "activity.fit", f.MakeFit(new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc)));
        await f.Upload(invalidId, "broken.fit", [1, 2, 3, 4]);

        var preview = await f.Dienst.Pruefen(new FitUploadPruefanfrage([validId, invalidId]), CancellationToken.None);
        var row = Assert.Single(preview.Zeilen);
        Assert.Equal(validId, row.UploadId);
        Assert.Equal("activity.fit", row.Dateiname);
        Assert.Equal(invalidId, Assert.Single(preview.Fehler).UploadId);
        Assert.Equal(FitUploadVersionen.Bestand([good]), preview.Version);

        var request = new FitUploadUebernahmeAnfrage(preview.Version, Guid.NewGuid(),
            [new FitUploadAuswahl(validId, row.DateiVersion, true, null)]);
        var result = await f.Dienst.Uebernehmen(request, CancellationToken.None);
        Assert.True(Assert.Single(result.Ergebnisse).Importiert);
        var tour = Assert.Single(ReadTours(f.Root));
        Assert.Equal(2, tour.Anhaenge.Count);
        Assert.Contains(tour.Anhaenge, x => x.RelativerPfad == $"2026/upload-{validId:N}.fit");
        Assert.True(File.Exists(f.Source(validId)));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(f.Source(validId))!, ".fit-review-*.tmp"));
        File.Delete(f.Source(validId));

        var replay = await f.Dienst.Uebernehmen(request, CancellationToken.None);
        Assert.Equal(Assert.Single(result.Ergebnisse).TourId, Assert.Single(replay.Ergebnisse).TourId);
        Assert.Single(ReadTours(f.Root));
    }

    [Fact]
    public async Task GeaenderteQuelldateiNachPreviewKonfligiertOhneImportOderQuittung()
    {
        using var f = new Fixture();
        var id = Guid.NewGuid();
        var upload = await f.Upload(id, "activity.fit", f.MakeFit(new DateTime(2026, 6, 2, 8, 0, 0, DateTimeKind.Utc)));
        var preview = await f.Dienst.Pruefen(new FitUploadPruefanfrage([id]), CancellationToken.None);
        var row = Assert.Single(preview.Zeilen);
        var request = new FitUploadUebernahmeAnfrage(preview.Version, Guid.NewGuid(),
            [new FitUploadAuswahl(id, row.DateiVersion, true, null)]);

        FitTestdatei.Schreibe(f.Source(id), new FitTestdatei.Werte(
            new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc), VerschiebungStunden: 0));
        var error = await Assert.ThrowsAsync<StammdatenException>(() => f.Dienst.Uebernehmen(request, CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.Konflikt, error.Art);
        Assert.Empty(ReadTours(f.Root));
        Assert.Equal(upload.Version, (await f.Store.Laden(id, CancellationToken.None))!.Version);
        using var db = OeffneDatenbank.Ausfuehren(Path.Combine(f.Root, "db.sqlite"));
        Assert.Equal(0, Scalar(db, "SELECT COUNT(*) FROM FitUploadQuittung;"));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(f.Source(id))!, ".fit-review-*.tmp"));
    }

    private static List<AndyTours.Core.Modell.Trainingseinheit> ReadTours(string root)
    {
        using var db = OeffneDatenbank.Ausfuehren(Path.Combine(root, "db.sqlite"));
        return TrainingseinheitenTabelle.Laden(db);
    }

    private static long Scalar(Microsoft.Data.Sqlite.SqliteConnection db, string sql)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
