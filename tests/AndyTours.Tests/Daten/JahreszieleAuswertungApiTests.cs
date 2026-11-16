using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

public sealed class JahreszieleAuswertungApiTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AndyTours-Zielprojektion-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AlleAktivitaetszieleWerdenInLongAggregiert()
    {
        Directory.CreateDirectory(_directory);
        var database = Path.Combine(_directory, "test.db");
        using (var connection = OeffneDatenbank.Ausfuehren(database))
        using (var transaction = connection.BeginTransaction())
        {
            AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "A", ZielKm = int.MaxValue });
            AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "B", ZielKm = int.MaxValue });
            transaction.Commit();
        }

        var result = await new SqliteAktivitaetsauswertung(database).Laden(new AktivitaetsauswertungAnfrage(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Auswertungsraster.Jahr, null, null));
        var goal = Assert.Single(result.Jahresziele);
        Assert.Equal(2026, goal.Jahr);
        Assert.Equal(4294967294L, goal.ZielKm);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
