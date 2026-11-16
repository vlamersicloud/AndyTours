using AndyTours.Application.Import;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed class SqliteTourenMehrfachLoeschenTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AndyTours-MehrfachLoeschen-" + Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(_directory, "andytours.db");

    [Fact]
    public async Task Bulk_delete_cleans_metadata_and_mappings_but_preserves_files_and_receipt_replay_after_id_reuse()
    {
        Directory.CreateDirectory(_directory);
        var attachmentDirectory = Path.Combine(_directory, "2026");
        Directory.CreateDirectory(attachmentDirectory);
        var firstFile = Path.Combine(attachmentDirectory, "first.fake");
        var secondFile = Path.Combine(attachmentDirectory, "second.fake");
        byte[] firstBytes = [1, 3, 5, 7];
        byte[] secondBytes = [2, 4, 6, 8];
        File.WriteAllBytes(firstFile, firstBytes);
        File.WriteAllBytes(secondFile, secondBytes);

        var firstUpload = Guid.NewGuid();
        var secondUpload = Guid.NewGuid();
        var uploadStore = new SqliteImportUploadSpeicher(Database);
        await uploadStore.Registrieren(firstUpload, "first.bin", new string('A', 64), 3, default);
        await uploadStore.Registrieren(secondUpload, "second.bin", new string('B', 64), 3, default);

        long firstId;
        long secondId;
        long activityId;
        using (var connection = OeffneDatenbank.Ausfuehren(Database))
        using (var transaction = connection.BeginTransaction())
        {
            var activity = AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "Rad", IstHaupt = true });
            activityId = activity;
            var category = KategorienTabelle.Einfuegen(connection, transaction, new Kategorie { Name = "Training" });
            var insertedFirst = TrainingseinheitenTabelle.Einfuegen(connection, transaction, new Trainingseinheit
            {
                Datum = new DateOnly(2026, 7, 1), Startzeit = new TimeOnly(8, 10), Name = "first",
                Fahrzeit = TimeSpan.FromMinutes(30), DistanzKm = 12, AktivitaetId = activity,
                KategorieIds = [category], Anhaenge = [new Anhang { Typ = AnhangTyp.GpsTrack, RelativerPfad = "2026/first.fake" }]
            }, (c, tx, id) => GarminImportZuordnung.Speichern(c, tx, 9001, id));
            var insertedSecond = TrainingseinheitenTabelle.Einfuegen(connection, transaction, new Trainingseinheit
            {
                Datum = new DateOnly(2026, 7, 2), Startzeit = new TimeOnly(9, 20), Name = "second",
                Fahrzeit = TimeSpan.FromMinutes(45), DistanzKm = 24, AktivitaetId = activity,
                KategorieIds = [category], Anhaenge = [new Anhang { Typ = AnhangTyp.GpsTrack, RelativerPfad = "2026/second.fake" }]
            }, (c, tx, id) => GarminImportZuordnung.Speichern(c, tx, 9002, id));
            firstId = insertedFirst.Id;
            secondId = insertedSecond.Id;
            transaction.Commit();
        }

        using (var connection = OeffneDatenbank.Ausfuehren(Database))
        using (var transaction = connection.BeginTransaction())
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "UPDATE FitUpload SET TourId = $tour WHERE UploadId = $upload;";
            command.Parameters.Add("$tour", SqliteType.Integer);
            command.Parameters.Add("$upload", SqliteType.Text);
            command.Parameters["$tour"].Value = firstId;
            command.Parameters["$upload"].Value = firstUpload.ToString("D");
            command.ExecuteNonQuery();
            command.Parameters["$tour"].Value = secondId;
            command.Parameters["$upload"].Value = secondUpload.ToString("D");
            command.ExecuteNonQuery();
            EinstellungTabelle.Schreiben(connection, transaction, "Unrelated.Preference", "preserve");
            transaction.Commit();
        }

        var tours = new SqliteTourenSpeicher(Database);
        var first = (await tours.Lesen(firstId))!;
        var second = (await tours.Lesen(secondId))!;
        var requestId = Guid.NewGuid();
        var request = new TourenLoeschenAnfrage(requestId,
            [new(first.Id, first.Version), new(second.Id, second.Version)]);
        var deletion = await new SqliteTourenMehrfachverwaltung(Database).Loeschen(request, default);

        Assert.Equal(requestId, deletion.AnfrageId);
        Assert.Empty(deletion.Eintraege);
        Assert.Equal(new[] { firstId, secondId }, deletion.GeloeschteIds);
        Assert.Null(await tours.Lesen(firstId));
        Assert.Null(await tours.Lesen(secondId));
        using (var verify = OeffneDatenbank.Ausfuehren(Database))
        {
            Assert.Equal(0, Scalar(verify, "SELECT COUNT(*) FROM EinheitKategorie;"));
            Assert.Equal(0, Scalar(verify, "SELECT COUNT(*) FROM Anhang;"));
            Assert.Null(EinstellungTabelle.Lesen(verify, "GarminConnect.Aktivitaet.9001"));
            Assert.Null(EinstellungTabelle.Lesen(verify, "GarminConnect.Aktivitaet.9002"));
            Assert.Equal("preserve", EinstellungTabelle.Lesen(verify, "Unrelated.Preference"));
            Assert.IsType<DBNull>(ReadNullable(verify, "SELECT TourId FROM FitUpload WHERE UploadId=$id;", firstUpload));
            Assert.IsType<DBNull>(ReadNullable(verify, "SELECT TourId FROM FitUpload WHERE UploadId=$id;", secondUpload));
        }
        Assert.Equal(firstBytes, File.ReadAllBytes(firstFile));
        Assert.Equal(secondBytes, File.ReadAllBytes(secondFile));

        var replay = await new SqliteTourenMehrfachverwaltung(Database).Loeschen(request, default);
        Assert.Equal(new[] { firstId, secondId }, replay.GeloeschteIds);

        long reusedId;
        using (var connection = OeffneDatenbank.Ausfuehren(Database))
        using (var transaction = connection.BeginTransaction())
        {
            reusedId = TrainingseinheitenTabelle.Einfuegen(connection, transaction,
                new Trainingseinheit { Datum = new DateOnly(2026, 7, 3), Name = "reused", AktivitaetId = activityId }).Id;
            transaction.Commit();
        }
        Assert.Equal(firstId, reusedId);
        var reused = (await tours.Lesen(reusedId))!;
        var replayAfterReuse = await new SqliteTourenMehrfachverwaltung(Database).Loeschen(request, default);
        Assert.Equal(new[] { firstId, secondId }, replayAfterReuse.GeloeschteIds);
        Assert.NotNull(await tours.Lesen(reusedId));
        Assert.Equal(1, ReceiptCount());
        await Assert.ThrowsAsync<TourVersionskonfliktException>(() => new SqliteTourenMehrfachverwaltung(Database).Loeschen(
            request with { Touren = [new(reusedId, reused.Version)] }, default));
        Assert.NotNull(await tours.Lesen(reusedId));
    }

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    private long ReceiptCount()
    {
        using var connection = OeffneDatenbank.Ausfuehren(Database);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ApiTourMehrfach;";
        return (long)command.ExecuteScalar()!;
    }

    private static object? ReadNullable(SqliteConnection connection, string sql, Guid uploadId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", uploadId.ToString("D"));
        return command.ExecuteScalar();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
