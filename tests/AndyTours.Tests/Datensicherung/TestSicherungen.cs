using Microsoft.Data.Sqlite;
using AndyTours.Daten;

namespace AndyTours.Tests.Datensicherung;

/// <summary>
/// Erzeugt für die Tests des Wiederherstellens besondere Sicherungsdateien
/// (ältere und neuere Schemaversion, fremde und beschädigte Dateien).
/// </summary>
public static class TestSicherungen
{
    /// <summary>
    /// Schreibt eine gültige Sicherung mit <paramref name="anzahlEinheiten"/> Einheiten des Namens <paramref name="name"/>.
    /// </summary>
    public static void Gueltig(string datei, int anzahlEinheiten, string name)
    {
        using SqliteConnection verbindung = OeffneDatenbank.Ausfuehren(datei);
        DatenbestandTabellen.Speichern(verbindung, TestDatenbestand.Erzeuge(anzahlEinheiten, name));
    }

    /// <summary>
    /// Schreibt eine Datenbank wie von AndyTours vor Schemaversion 5: mit den später entfernten Spalten
    /// (gefüllt mit Beispielwerten) und der Tabelle <c>Schwellwert</c>.
    /// </summary>
    public static void AlteVersion(string datei, int version, int anzahlEinheiten, string name)
    {
        Gueltig(datei, anzahlEinheiten, name);
        Fuehre(datei, $"""
            ALTER TABLE Equipment ADD COLUMN GewichtKg REAL NOT NULL DEFAULT 13.5;
            ALTER TABLE Equipment ADD COLUMN RadumfangMm INTEGER NOT NULL DEFAULT 2210;
            ALTER TABLE Equipment ADD COLUMN CrCoeff REAL NOT NULL DEFAULT 0.004;
            ALTER TABLE Equipment ADD COLUMN CmCoeff REAL NOT NULL DEFAULT 0;
            ALTER TABLE Equipment ADD COLUMN CwCoeff REAL NOT NULL DEFAULT 0.9;
            ALTER TABLE Equipment ADD COLUMN CwaCoeff REAL NOT NULL DEFAULT 0.5;
            ALTER TABLE Equipment ADD COLUMN SinCoeff REAL NOT NULL DEFAULT 0;
            ALTER TABLE Equipment ADD COLUMN Kettenblaetter TEXT NOT NULL DEFAULT '24,34';
            ALTER TABLE Equipment ADD COLUMN Ritzel TEXT NOT NULL DEFAULT '11,36';
            ALTER TABLE Equipment ADD COLUMN Bildpfad TEXT NOT NULL DEFAULT 'rad.jpg';
            ALTER TABLE Fahrer ADD COLUMN TrainingspulsVon INTEGER NOT NULL DEFAULT 120;
            ALTER TABLE Fahrer ADD COLUMN TrainingspulsBis INTEGER NOT NULL DEFAULT 150;
            ALTER TABLE Fahrer ADD COLUMN HeimatBreitengrad REAL DEFAULT 48.76;
            ALTER TABLE Fahrer ADD COLUMN HeimatLaengengrad REAL DEFAULT 11.42;
            CREATE TABLE Schwellwert (Id INTEGER PRIMARY KEY, Art TEXT NOT NULL, Reihenfolge INTEGER NOT NULL,
                Name TEXT NOT NULL, Von REAL NOT NULL, Bis REAL NOT NULL, Farbe TEXT NOT NULL);
            INSERT INTO Schwellwert VALUES (1, 'Steigung', 0, 'Flach', 0, 3, '#AA0000');
            PRAGMA user_version = {version};
            """);
    }

    /// <summary>
    /// Schreibt eine Sicherung wie von AndyTours mit Schemaversion 1: zusätzlich ohne Tabelle <c>Einstellung</c>.
    /// </summary>
    public static void Version1(string datei, int anzahlEinheiten, string name)
    {
        AlteVersion(datei, 1, anzahlEinheiten, name);
        Fuehre(datei, "DROP TABLE Einstellung;");
    }

    /// <summary>Spaltennamen einer Tabelle.</summary>
    public static List<string> Spalten(string datei, string tabelle)
    {
        using SqliteConnection verbindung = Verbindung(datei);
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.CommandText = $"SELECT name FROM pragma_table_info('{tabelle}');";
        using SqliteDataReader leser = befehl.ExecuteReader();
        var spalten = new List<string>();
        while (leser.Read())
        {
            spalten.Add(leser.GetString(0));
        }

        return spalten;
    }

    /// <summary>Schemaversion (<c>PRAGMA user_version</c>) einer Datei, ohne sie umzubauen.</summary>
    public static long Version(string datei)
    {
        using SqliteConnection verbindung = Verbindung(datei);
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.CommandText = "PRAGMA user_version;";
        return (long)befehl.ExecuteScalar()!;
    }

    /// <summary>Schreibt eine Sicherung mit der angegebenen Schemaversion (z. B. eine höhere als die aktuelle = neuere Programmversion).</summary>
    public static void MitVersion(string datei, int version)
    {
        Gueltig(datei, 1, "Zukunft");
        Fuehre(datei, $"PRAGMA user_version = {version};");
    }

    /// <summary>Schreibt eine SQLite-Datenbank, die nichts mit AndyTours zu tun hat.</summary>
    public static void Fremd(string datei) => Fuehre(datei, "CREATE TABLE Adresse (Name TEXT);");

    /// <summary>Schreibt eine gültige Sicherung und schneidet sie danach auf die halbe Länge ab.</summary>
    public static void Abgeschnitten(string datei)
    {
        Gueltig(datei, 30, "Abgeschnitten");
        byte[] inhalt = File.ReadAllBytes(datei);
        File.WriteAllBytes(datei, inhalt[..(inhalt.Length / 2)]);
    }

    /// <summary>Führt SQL direkt auf der Datei aus, ohne Schema anzulegen.</summary>
    public static void Fuehre(string datei, string sql)
    {
        using SqliteConnection verbindung = Verbindung(datei);
        using SqliteCommand befehl = verbindung.CreateCommand();
        befehl.CommandText = sql;
        befehl.ExecuteNonQuery();
    }

    /// <summary>Öffnet die Datei direkt, ohne Schema anzulegen oder umzubauen.</summary>
    private static SqliteConnection Verbindung(string datei)
    {
        var verbindung = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = datei, Pooling = false }.ToString());
        verbindung.Open();
        return verbindung;
    }
}
