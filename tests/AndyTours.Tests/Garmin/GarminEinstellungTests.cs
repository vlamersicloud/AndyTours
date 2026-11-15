using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Garmin;

/// <summary>Speichern, Lesen und Entfernen persönlicher Garmin-Einstellungen ohne echte Zugangsdaten.</summary>
public sealed class GarminEinstellungTests
{
    [Fact]
    public void OhneEinstellungIstKeinKeyHinterlegt()
    {
        using SqliteConnection db = Oeffne();
        Assert.Null(GarminEinstellung.Laden(db));
    }

    [Fact]
    public void SpeichernBereinigtRandLeerzeichenUndErsetztDenBisherigenWert()
    {
        using SqliteConnection db = Oeffne();
        GarminEinstellung.Speichern(db, "  test-key  ");
        Assert.Equal("test-key", GarminEinstellung.Laden(db));
        GarminEinstellung.Speichern(db, "replacement-key");
        Assert.Equal("replacement-key", GarminEinstellung.Laden(db));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EntfernenLoeschtDenEintragUndLaesstAndereEinstellungenUnberuehrt(string? leer)
    {
        using SqliteConnection db = Oeffne();
        EinstellungTabelle.Schreiben(db, null, "Andere.Einstellung", "bleibt");
        GarminEinstellung.Speichern(db, "test-key");
        GarminEinstellung.Speichern(db, leer);
        Assert.Null(GarminEinstellung.Laden(db));
        Assert.Single(EinstellungTabelle.LesenMitVorsilbe(db, ""));
        Assert.Equal("bleibt", EinstellungTabelle.Lesen(db, "Andere.Einstellung"));
    }

    [Fact]
    public void DatenordnerHabenGetrennteKeys()
    {
        using SqliteConnection erster = Oeffne();
        using SqliteConnection zweiter = Oeffne();
        GarminEinstellung.Speichern(erster, "first-key");
        Assert.Null(GarminEinstellung.Laden(zweiter));
        GarminEinstellung.Speichern(zweiter, "second-key");
        Assert.Equal("first-key", GarminEinstellung.Laden(erster));
        Assert.Equal("second-key", GarminEinstellung.Laden(zweiter));
    }

    [Fact]
    public void AutomatikIstStandardmaessigAusUndWirdUnabhaengigVomKeyGespeichert()
    {
        using SqliteConnection db = Oeffne();
        Assert.False(GarminEinstellung.AutomatischLaden(db));
        GarminEinstellung.AutomatischSpeichern(db, true);
        Assert.True(GarminEinstellung.AutomatischLaden(db));
        GarminEinstellung.Speichern(db, "test-key");
        GarminEinstellung.Speichern(db, null);
        Assert.True(GarminEinstellung.AutomatischLaden(db));
        GarminEinstellung.AutomatischSpeichern(db, false);
        Assert.False(GarminEinstellung.AutomatischLaden(db));
        Assert.Null(EinstellungTabelle.Lesen(db, Einstellungsschluessel.GarminAllePaketeAutomatisch));
    }

    private static SqliteConnection Oeffne()
    {
        var db = new SqliteConnection("Data Source=:memory:");
        db.Open();
        using SqliteCommand befehl = db.CreateCommand();
        befehl.CommandText = "CREATE TABLE Einstellung (Schluessel TEXT PRIMARY KEY, Wert TEXT NOT NULL);";
        befehl.ExecuteNonQuery();
        return db;
    }
}
