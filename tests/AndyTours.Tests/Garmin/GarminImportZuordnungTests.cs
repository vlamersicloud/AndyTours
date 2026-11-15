using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Garmin;

/// <summary>Prüft persistente IDs und den Schutz innerhalb der Schreibtransaktion ohne echte Benutzerdateien.</summary>
public sealed class GarminImportZuordnungTests
{
    [Fact]
    public void ZuordnungWirdNurMitTransaktionGespeichertUndGeloeschteEinheitNichtUebersprungen()
    {
        using SqliteConnection db = Oeffne();
        using (SqliteTransaction tx = db.BeginTransaction())
        {
            GarminImportZuordnung.Speichern(db, tx, 100, 7);
            tx.Rollback();
        }

        Assert.Empty(GarminImportZuordnung.Laden(db, new HashSet<long> { 7 }));
        using (SqliteTransaction tx = db.BeginTransaction())
        {
            GarminImportZuordnung.Speichern(db, tx, 100, 7);
            tx.Commit();
        }

        Assert.Contains(100L, GarminImportZuordnung.Laden(db, new HashSet<long> { 7 }));
        Assert.Empty(GarminImportZuordnung.Laden(db, new HashSet<long>()));
    }

    [Fact]
    public void DieselbeGarminIdDarfKeineZweiteEinheitErzeugen()
    {
        using SqliteConnection db = Oeffne();
        using (SqliteTransaction tx = db.BeginTransaction())
        {
            Schreibe(db, tx, "INSERT INTO Trainingseinheit VALUES (7);");
            GarminImportZuordnung.Speichern(db, tx, 100, 7);
            tx.Commit();
        }

        using (SqliteTransaction tx = db.BeginTransaction())
        {
            // Wie im echten Einfügeablauf existiert die neue Zeile zunächst unbestätigt.
            Schreibe(db, tx, "INSERT INTO Trainingseinheit VALUES (8);");
            Assert.Throws<InvalidOperationException>(() => GarminImportZuordnung.Speichern(db, tx, 100, 8));
            tx.Rollback();
        }

        using SqliteCommand befehl = db.CreateCommand();
        befehl.CommandText = "SELECT COUNT(*) FROM Trainingseinheit;";
        Assert.Equal(1L, (long)befehl.ExecuteScalar()!);
        Assert.Equal("7", EinstellungTabelle.Lesen(db, "GarminConnect.Aktivitaet.100"));
    }

    [Fact]
    public void NachLoeschenDerAltenEinheitIstErneuterImportMoeglich()
    {
        using SqliteConnection db = Oeffne();
        using (SqliteTransaction tx = db.BeginTransaction())
        {
            Schreibe(db, tx, "INSERT INTO Trainingseinheit VALUES (7);");
            GarminImportZuordnung.Speichern(db, tx, 100, 7);
            tx.Commit();
        }

        using (SqliteTransaction tx = db.BeginTransaction())
        {
            Schreibe(db, tx, "DELETE FROM Trainingseinheit WHERE Id = 7; INSERT INTO Trainingseinheit VALUES (8);");
            GarminImportZuordnung.Speichern(db, tx, 100, 8);
            tx.Commit();
        }

        Assert.Equal("8", EinstellungTabelle.Lesen(db, "GarminConnect.Aktivitaet.100"));
    }

    private static SqliteConnection Oeffne()
    {
        var db = new SqliteConnection("Data Source=:memory:");
        db.Open();
        using SqliteCommand befehl = db.CreateCommand();
        befehl.CommandText = """
            CREATE TABLE Einstellung (Schluessel TEXT PRIMARY KEY, Wert TEXT NOT NULL);
            CREATE TABLE Trainingseinheit (Id INTEGER PRIMARY KEY);
            """;
        befehl.ExecuteNonQuery();
        return db;
    }

    private static void Schreibe(SqliteConnection db, SqliteTransaction tx, string sql)
    {
        using SqliteCommand befehl = db.CreateCommand();
        befehl.Transaction = tx;
        befehl.CommandText = sql;
        befehl.ExecuteNonQuery();
    }
}
