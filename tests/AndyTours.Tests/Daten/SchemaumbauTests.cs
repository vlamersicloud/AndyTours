using Microsoft.Data.Sqlite;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Daten.Datenordner;
using AndyTours.Tests.Datenordner;
using AndyTours.Tests.Datensicherung;

namespace AndyTours.Tests.Daten;

/// <summary>
/// Tests für den Umbau älterer Datenbanken beim Öffnen (<see cref="OeffneDatenbank"/>, <see cref="BaueSchemaUm"/>).
/// </summary>
/// <remarks>
/// Jeder Test legt einen Datenordner mit einer Datenbank der Schemaversion 4 an: 3 Einheiten „Alt“ und die
/// später entfernten Spalten samt Tabelle <c>Schwellwert</c>.
/// </remarks>
public class SchemaumbauTests
{
    /// <summary>Legt die Datenbank der Version 4 im Datenordner an und liefert ihren Pfad.</summary>
    private static string LegeVersion4An(string datenordner)
    {
        string datei = Path.Combine(datenordner, DatenordnerInhalt.Datenbankdatei);
        TestSicherungen.AlteVersion(datei, 4, 3, "Alt");
        return datei;
    }

    /// <summary>Öffnet die Datenbank (mit Umbau) und schließt sie gleich wieder.</summary>
    private static void Oeffne(string datei)
    {
        using SqliteConnection verbindung = OeffneDatenbank.Ausfuehren(datei);
    }

    [Fact]
    public void Neue_Datenbank_hat_die_aktuelle_Version_ohne_entfernte_Werte()
    {
        using var temp = new TempOrdner();
        string datei = Path.Combine(temp.Pfad, DatenordnerInhalt.Datenbankdatei);

        Oeffne(datei);

        Assert.Equal(ErstelleSchema.Schemaversion, TestSicherungen.Version(datei));
        Assert.DoesNotContain("GewichtKg", TestSicherungen.Spalten(datei, "Equipment"));
        Assert.Empty(TestSicherungen.Spalten(datei, "Schwellwert"));
        Assert.False(Directory.Exists(Path.Combine(temp.Pfad, "Sicherungen")));
    }

    [Fact]
    public void Umbau_entfernt_die_Werte_und_behaelt_alle_uebrigen_Daten()
    {
        using var temp = new TempOrdner();
        string datei = LegeVersion4An(temp.Pfad);
        List<string> equipmentVorher = TestSicherungen.Spalten(datei, "Equipment");
        List<string> fahrerVorher = TestSicherungen.Spalten(datei, "Fahrer");

        Oeffne(datei);

        Assert.Equal(ErstelleSchema.Schemaversion, TestSicherungen.Version(datei));
        string[] entferntEquipment = ["GewichtKg", "RadumfangMm", "CrCoeff", "CmCoeff", "CwCoeff", "CwaCoeff", "SinCoeff", "Kettenblaetter", "Ritzel", "Bildpfad"];
        string[] entferntFahrer = ["TrainingspulsVon", "TrainingspulsBis", "HeimatBreitengrad", "HeimatLaengengrad"];
        Assert.Equal(equipmentVorher.Except(entferntEquipment), TestSicherungen.Spalten(datei, "Equipment"));
        Assert.Equal(fahrerVorher.Except(entferntFahrer), TestSicherungen.Spalten(datei, "Fahrer"));
        Assert.Empty(TestSicherungen.Spalten(datei, "Schwellwert"));

        // Alle übrigen Daten sind unverändert vorhanden und lassen sich laden
        Datenbestand vorher = TestDatenbestand.Erzeuge(3, "Alt");
        Datenbestand nachher = TestDatenbestand.Lade(datei);
        Assert.Equal(vorher.Trainingseinheiten.Select(e => (e.Name, e.Datum, e.DistanzKm)), nachher.Trainingseinheiten.Select(e => (e.Name, e.Datum, e.DistanzKm)));
        Assert.Equal(vorher.Equipments.Select(e => e.Name), nachher.Equipments.Select(e => e.Name));
        Assert.Equal(vorher.Fahrer.Select(f => f.Vorname), nachher.Fahrer.Select(f => f.Vorname));
    }

    [Fact]
    public void Vor_dem_Umbau_wird_der_bisherige_Stand_gesichert()
    {
        using var temp = new TempOrdner();
        string datei = LegeVersion4An(temp.Pfad);

        Oeffne(datei);

        string sicherung = Assert.Single(Directory.GetFiles(Path.Combine(temp.Pfad, "Sicherungen"), "*.db"));
        Assert.Equal(4, TestSicherungen.Version(sicherung));
        Assert.Contains("Kettenblaetter", TestSicherungen.Spalten(sicherung, "Equipment"));
    }

    [Fact]
    public void Scheitert_die_Sicherung_wird_nicht_umgebaut()
    {
        using var temp = new TempOrdner();
        string datei = LegeVersion4An(temp.Pfad);
        // Sicherungsordner zeigt auf eine vorhandene Datei: Der Ordner lässt sich nicht anlegen
        string blockiert = Path.Combine(temp.Pfad, "keinOrdner");
        File.WriteAllText(blockiert, "");
        TestSicherungen.Fuehre(datei, $"INSERT INTO Einstellung VALUES ('{Einstellungsschluessel.Sicherungsordner}', '{blockiert}');");

        var fehler = Assert.Throws<SicherungVorUmbauFehlgeschlagenException>(() => Oeffne(datei));

        Assert.Equal(blockiert, fehler.Sicherungsordner);
        Assert.Equal(4, TestSicherungen.Version(datei));
        Assert.Contains("Kettenblaetter", TestSicherungen.Spalten(datei, "Equipment"));
    }

    [Fact]
    public void Scheitert_eine_Umbaustufe_bleibt_die_Datenbank_unveraendert()
    {
        using var temp = new TempOrdner();
        string datei = LegeVersion4An(temp.Pfad);
        // Ohne die Spalte Bildpfad scheitert die Stufe auf Version 5 mitten im Umbau
        TestSicherungen.Fuehre(datei, "ALTER TABLE Equipment DROP COLUMN Bildpfad;");

        Assert.Throws<SqliteException>(() => Oeffne(datei));

        // Die vorher entfernten Spalten sind wieder da: Die Transaktion wurde vollständig verworfen
        Assert.Equal(4, TestSicherungen.Version(datei));
        Assert.Contains("GewichtKg", TestSicherungen.Spalten(datei, "Equipment"));
        Assert.Contains("TrainingspulsVon", TestSicherungen.Spalten(datei, "Fahrer"));
        Assert.NotEmpty(TestSicherungen.Spalten(datei, "Schwellwert"));
    }
}
