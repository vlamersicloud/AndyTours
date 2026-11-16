using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed class SqliteTourenMehrfachRollbackTests : IDisposable
{
    private long _activity;
    private long _otherActivity;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AndyTours-MehrfachRollback-" + Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(_directory, "andytours.db");

    [Fact]
    public async Task Receipt_insert_failure_rolls_back_mutation_and_same_request_can_retry()
    {
        Directory.CreateDirectory(_directory);
        var first = await Seed("Erste");
        var second = await Seed("Zweite");
        var bulk = new SqliteTourenMehrfachverwaltung(Database);
        var store = new SqliteTourenSpeicher(Database);
        var baseline = await bulk.Kategorien(new TourenKategorienAnfrage(Guid.NewGuid(),
            [new(first.Id, first.Version)], [], []), default);
        Assert.Single(baseline.Eintraege);
        Assert.Equal(1, ReceiptCount());

        var requestId = Guid.NewGuid();
        var request = new TourenAktivitaetAnfrage(requestId,
            [new(first.Id, first.Version), new(second.Id, second.Version)], _otherActivity);
        Execute("CREATE TRIGGER FailBulkReceipt BEFORE INSERT ON ApiTourMehrfach BEGIN SELECT RAISE(ABORT, 'synthetic'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => bulk.Aktivitaet(request, default));
        Assert.Equal(first.Version, (await store.Lesen(first.Id))!.Version);
        Assert.Equal(second.Version, (await store.Lesen(second.Id))!.Version);
        Assert.Equal("Erste", (await store.Lesen(first.Id))!.Werte.Name);
        Assert.Equal("Zweite", (await store.Lesen(second.Id))!.Werte.Name);
        Assert.Equal(1, ReceiptCount());

        Execute("DROP TRIGGER FailBulkReceipt;");
        var retry = await bulk.Aktivitaet(request, default);
        Assert.Equal(requestId, retry.AnfrageId);
        Assert.Equal(2, retry.Eintraege.Count);
        Assert.Equal(2, ReceiptCount());
    }

    [Fact]
    public async Task Corrupt_receipts_are_rejected_without_echoing_persisted_json()
    {
        Directory.CreateDirectory(_directory);
        var tour = await Seed("Tour");
        var bulk = new SqliteTourenMehrfachverwaltung(Database);
        var requestId = Guid.NewGuid();
        var request = new TourenKategorienAnfrage(requestId, [new(tour.Id, tour.Version)], [], []);
        var receipt = await bulk.Kategorien(request, default);

        SetReceipt(requestId, System.Text.Json.JsonSerializer.Serialize(new TourenMehrfachAntwort(requestId, [], [])));
        var emptyResponse = await Assert.ThrowsAsync<InvalidOperationException>(() => bulk.Kategorien(request, default));
        Assert.DoesNotContain("{}", emptyResponse.Message);
        Assert.Equal(tour.Version, (await new SqliteTourenSpeicher(Database).Lesen(tour.Id))!.Version);

        SetReceipt(requestId, "not-json-secret");
        var invalidJson = await Assert.ThrowsAsync<InvalidOperationException>(() => bulk.Kategorien(request, default));
        Assert.DoesNotContain("not-json-secret", invalidJson.Message);
        Assert.Equal(1, ReceiptCount());
        _ = receipt;
    }

    private async Task<TourAntwort> Seed(string name)
    {
        using (var connection = OeffneDatenbank.Ausfuehren(Database))
        using (var transaction = connection.BeginTransaction())
        {
            if (_activity == 0) { _activity = AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "Rad", IstHaupt = true }); _otherActivity = AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "Laufen" }); }
            transaction.Commit();
        }
        return await new SqliteTourenSpeicher(Database).Anlegen(TourenMapper.ToAntwort(new Trainingseinheit
        {
            Datum = new DateOnly(2026, 7, 1), Name = name, AktivitaetId = _activity,
            Fahrzeit = TimeSpan.FromMinutes(20), DistanzKm = 10
        }).Werte);
    }

    private void Execute(string sql)
    {
        using var connection = OeffneDatenbank.Ausfuehren(Database);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private void SetReceipt(Guid id, string json)
    {
        using var connection = OeffneDatenbank.Ausfuehren(Database);
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ApiTourMehrfach SET AntwortJson = @json WHERE AnfrageId = @id;";
        command.Parameters.AddWithValue("@json", json);
        command.Parameters.AddWithValue("@id", id.ToString("D"));
        command.ExecuteNonQuery();
    }

    private long ReceiptCount()
    {
        using var connection = OeffneDatenbank.Ausfuehren(Database);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ApiTourMehrfach;";
        return (long)command.ExecuteScalar()!;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
