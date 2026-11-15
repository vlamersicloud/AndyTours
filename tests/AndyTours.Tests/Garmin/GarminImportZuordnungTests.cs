using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Garmin;

/// <summary>Prüft persistente IDs ohne echte Benutzerdateien.</summary>
public sealed class GarminImportZuordnungTests
{
    [Fact]
    public void ZuordnungWirdNurMitTransaktionGespeichertUndGeloeschteEinheitNichtUebersprungen()
    {
        using var verbindung = new SqliteConnection("Data Source=:memory:");
        verbindung.Open();
        using (var befehl = verbindung.CreateCommand())
        {
            befehl.CommandText = "CREATE TABLE Einstellung (Schluessel TEXT PRIMARY KEY, Wert TEXT NOT NULL);";
            befehl.ExecuteNonQuery();
        }

        using (var transaktion = verbindung.BeginTransaction())
        {
            GarminImportZuordnung.Speichern(verbindung, transaktion, 100, 7);
            transaktion.Rollback();
        }

        Assert.Empty(GarminImportZuordnung.Laden(verbindung, new HashSet<long> { 7 }));
        using (var transaktion = verbindung.BeginTransaction())
        {
            GarminImportZuordnung.Speichern(verbindung, transaktion, 100, 7);
            transaktion.Commit();
        }

        Assert.Contains(100L, GarminImportZuordnung.Laden(verbindung, new HashSet<long> { 7 }));
        Assert.Empty(GarminImportZuordnung.Laden(verbindung, new HashSet<long>()));
    }
}
