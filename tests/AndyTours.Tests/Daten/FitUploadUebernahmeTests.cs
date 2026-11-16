using AndyTours.Application.Import;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndyTours.Tests.Daten;

public sealed class FitUploadUebernahmeTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "FitUploadCommit-" + Guid.NewGuid().ToString("N"));
        public string DbPath => Path.Combine(_root, "db.sqlite");
        public SqliteImportUploadSpeicher Store { get; }
        public Fixture()
        {
            Directory.CreateDirectory(_root);
            using (var db = OeffneDatenbank.Ausfuehren(DbPath))
            using (var tx = db.BeginTransaction())
            {
                AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Rad", IstHaupt = true });
                tx.Commit();
            }
            Store = new SqliteImportUploadSpeicher(DbPath);
        }
        public async Task<(Guid Id, FitUploadAntwort Upload)> Register(string name)
        {
            var id = Guid.NewGuid();
            var upload = await Store.Registrieren(id, name, new string('A', 64), 100, CancellationToken.None);
            return (id, upload);
        }
        public void Dispose() { try { Directory.Delete(_root, true); } catch (IOException) { } }
    }

    [Fact]
    public async Task ImportiertMitInterpretationsTokenUndReplaytOhneVorbereitung()
    {
        using var f = new Fixture();
        var (id, _) = await f.Register("ride.bin");
        var snapshot = await f.Store.Pruefbestand([id], CancellationToken.None);
        var interpretationToken = new string('F', 64);
        var requestId = Guid.NewGuid();
        var request = new FitUploadUebernahmeAnfrage(snapshot.Version, requestId,
            [new FitUploadAuswahl(id, interpretationToken, true, null)]);
        var record = new Aufzeichnung
        {
            Datum = new DateOnly(2026, 4, 1),
            Startzeit = new TimeOnly(10, 30),
            Fahrzeit = TimeSpan.FromHours(1),
            DistanzKm = 20,
            Dateiname = "ride.bin"
        };
        var prep = new FitUploadVorbereitung(id, record, $"2026/upload-{id:N}.fit");
        var result = await f.Store.Uebernehmen(request, [prep], CancellationToken.None);

        var imported = Assert.Single(result.Ergebnisse);
        Assert.True(imported.Importiert);
        var tourId = Assert.IsType<long>(imported.TourId);
        using (var db = OeffneDatenbank.Ausfuehren(f.DbPath))
        {
            var tour = Assert.Single(TrainingseinheitenTabelle.Laden(db));
            Assert.Equal(tourId, tour.Id);
            Assert.Single(FahrerTabelle.Laden(db));
            Assert.Equal(2, tour.Anhaenge.Count);
            Assert.Equal(0, Scalar(db, "SELECT COUNT(*) FROM GarminImportZuordnung;"));
            Assert.Equal((int)FitUploadStatus.Importiert, (int)Enum.Parse<FitUploadStatus>(ReadScalar(db, "SELECT Zustand FROM FitUpload WHERE UploadId=$id;", id)));
            Assert.Equal(tourId, long.Parse(ReadScalar(db, "SELECT TourId FROM FitUpload WHERE UploadId=$id;", id), System.Globalization.CultureInfo.InvariantCulture));
        }

        var replay = await f.Store.Uebernehmen(request, [], CancellationToken.None);
        Assert.Equal(tourId, Assert.Single(replay.Ergebnisse).TourId);
        using (var db = OeffneDatenbank.Ausfuehren(f.DbPath))
            Assert.Single(TrainingseinheitenTabelle.Laden(db));

        var changed = request with { Auswahl = [new FitUploadAuswahl(id, interpretationToken, false, null)] };
        var error = await Assert.ThrowsAsync<StammdatenException>(() => f.Store.Uebernehmen(changed, [], CancellationToken.None));
        Assert.Equal(StammdatenFehlerart.Konflikt, error.Art);
    }

    [Fact]
    public async Task ZweiterTourInsertFehlerRolltBatchUndQuittungVollstaendigZurueck()
    {
        using var f = new Fixture();
        var (firstId, _) = await f.Register("first.fit");
        var (secondId, _) = await f.Register("second.fit");
        var snapshot = await f.Store.Pruefbestand([firstId, secondId], CancellationToken.None);
        var beforeFirst = (await f.Store.Laden(firstId, CancellationToken.None))!.Version;
        var beforeSecond = (await f.Store.Laden(secondId, CancellationToken.None))!.Version;

        using (var db = OeffneDatenbank.Ausfuehren(f.DbPath))
        using (var command = db.CreateCommand())
        {
            command.CommandText = "CREATE TRIGGER FailSecond BEFORE INSERT ON Trainingseinheit WHEN NEW.Datum='2026-04-02' BEGIN SELECT RAISE(ABORT,'fixture_failure'); END;";
            command.ExecuteNonQuery();
        }

        var request = new FitUploadUebernahmeAnfrage(snapshot.Version, Guid.NewGuid(), [
            new FitUploadAuswahl(firstId, new string('F', 64), true, null),
            new FitUploadAuswahl(secondId, new string('E', 64), true, null)]);
        var prepared = new[] {
            new FitUploadVorbereitung(firstId, new Aufzeichnung { Datum = new DateOnly(2026, 4, 1), Startzeit = new TimeOnly(10, 0), Fahrzeit = TimeSpan.FromHours(1), DistanzKm = 20 }, $"2026/upload-{firstId:N}.fit"),
            new FitUploadVorbereitung(secondId, new Aufzeichnung { Datum = new DateOnly(2026, 4, 2), Startzeit = new TimeOnly(10, 0), Fahrzeit = TimeSpan.FromHours(1), DistanzKm = 20 }, $"2026/upload-{secondId:N}.fit")};
        await Assert.ThrowsAsync<SqliteException>(() => f.Store.Uebernehmen(request, prepared, CancellationToken.None));

        using var verify = OeffneDatenbank.Ausfuehren(f.DbPath);
        Assert.Empty(TrainingseinheitenTabelle.Laden(verify));
        Assert.Empty(FahrerTabelle.Laden(verify));
        Assert.Equal(0, Scalar(verify, "SELECT COUNT(*) FROM Anhang;"));
        Assert.Equal(0, Scalar(verify, "SELECT COUNT(*) FROM FitUploadQuittung;"));
        Assert.Equal(beforeFirst, (await f.Store.Laden(firstId, CancellationToken.None))!.Version);
        Assert.Equal(beforeSecond, (await f.Store.Laden(secondId, CancellationToken.None))!.Version);
        Assert.Equal((int)FitUploadStatus.Hochgeladen, Scalar(verify, "SELECT Zustand FROM FitUpload WHERE UploadId='" + firstId.ToString("D") + "';"));
        Assert.Equal((int)FitUploadStatus.Hochgeladen, Scalar(verify, "SELECT Zustand FROM FitUpload WHERE UploadId='" + secondId.ToString("D") + "';"));
    }

    private static long Scalar(SqliteConnection db, string sql)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ReadScalar(SqliteConnection db, string sql, Guid id)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture)!;
    }
}
