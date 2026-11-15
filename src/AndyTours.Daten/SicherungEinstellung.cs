using System.Globalization;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>
/// Liest und schreibt die Einstellungen der automatischen Datensicherung.
/// </summary>
public static class SicherungEinstellung
{
    /// <summary>
    /// Liest Sicherungsordner, Intervall, Höchstzahl und Startzähler.
    /// </summary>
    /// <remarks>
    /// Fehlende oder unbrauchbare Einträge (z. B. von Hand in der Datenbank verändert) werden
    /// durch die Standardwerte aus <see cref="Sicherungseinstellungen"/> ersetzt – eine neue oder
    /// ältere Datenbank ohne Einträge liefert also genau die Standardeinstellungen.
    /// </remarks>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <returns>Die Einstellungen.</returns>
    public static Sicherungseinstellungen Laden(SqliteConnection verbindung)
    {
        Dictionary<string, string> werte = EinstellungTabelle.LesenMitVorsilbe(verbindung, "Sicherung.");

        var standard = new Sicherungseinstellungen();
        string? ordner = werte.GetValueOrDefault(Einstellungsschluessel.Sicherungsordner);
        int starts = int.TryParse(werte.GetValueOrDefault(Einstellungsschluessel.StartsSeitSicherung),
            NumberStyles.None, CultureInfo.InvariantCulture, out int s) ? s : 0;

        return new Sicherungseinstellungen
        {
            Sicherungsordner = string.IsNullOrWhiteSpace(ordner) ? null : ordner,
            // Intervall und Höchstzahl unterliegen denselben Grenzen wie die Eingabe in den Einstellungen
            Intervall = PruefeSicherungswert.Ausfuehren(werte.GetValueOrDefault(Einstellungsschluessel.Sicherungsintervall)) ?? standard.Intervall,
            Hoechstzahl = PruefeSicherungswert.Ausfuehren(werte.GetValueOrDefault(Einstellungsschluessel.Sicherungshoechstzahl)) ?? standard.Hoechstzahl,
            StartsSeitSicherung = starts,
        };
    }

    /// <summary>
    /// Schreibt alle Werte; vorhandene Einträge werden ersetzt.
    /// </summary>
    /// <remarks>
    /// Ist kein eigener Sicherungsordner gewählt (<c>null</c>), wird der Eintrag gelöscht – dann gilt
    /// wieder der Standardordner. Alle Änderungen laufen in einer Transaktion, damit nie nur ein Teil
    /// der Einstellungen gespeichert ist.
    /// </remarks>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <param name="einstellungen">Die zu speichernden Einstellungen.</param>
    public static void Speichern(SqliteConnection verbindung, Sicherungseinstellungen einstellungen)
    {
        using SqliteTransaction transaktion = verbindung.BeginTransaction();

        EinstellungTabelle.Schreiben(verbindung, transaktion, Einstellungsschluessel.Sicherungsordner, einstellungen.Sicherungsordner);
        EinstellungTabelle.Schreiben(verbindung, transaktion, Einstellungsschluessel.Sicherungsintervall, AlsText(einstellungen.Intervall));
        EinstellungTabelle.Schreiben(verbindung, transaktion, Einstellungsschluessel.Sicherungshoechstzahl, AlsText(einstellungen.Hoechstzahl));
        EinstellungTabelle.Schreiben(verbindung, transaktion, Einstellungsschluessel.StartsSeitSicherung, AlsText(einstellungen.StartsSeitSicherung));

        transaktion.Commit();
    }

    /// <summary>Ganze Zahl als Text ohne Tausenderpunkte (unabhängig von der Spracheinstellung).</summary>
    private static string AlsText(int zahl) => zahl.ToString(CultureInfo.InvariantCulture);
}
