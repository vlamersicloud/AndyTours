using System.Globalization;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>
/// Liest und schreibt die vom Benutzer festgelegte Reihenfolge der Aktivitäten.
/// </summary>
/// <remarks>
/// Die Reihenfolge steht als Liste der Ids in der Tabelle <c>Einstellung</c> unter
/// <see cref="Einstellungsschluessel.AktivitaetenReihenfolge"/> – so übersteht sie ein Umbenennen und
/// braucht keine Änderung des Datenbankschemas. Hergestellt wird die Reihenfolge mit
/// <see cref="AndyTours.Core.Berechnungen.OrdneAktivitaeten"/>.
/// </remarks>
public static class AktivitaetenReihenfolgeEinstellung
{
    /// <summary>Trennzeichen zwischen den Ids.</summary>
    private const char Trenner = ';';

    /// <summary>
    /// Liest die gespeicherte Reihenfolge.
    /// </summary>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <returns>Die Ids in der Reihenfolge des Benutzers; leer, wenn nie gespeichert. Ungültige Teile werden übergangen.</returns>
    public static List<long> Laden(SqliteConnection verbindung)
    {
        string? wert = EinstellungTabelle.Lesen(verbindung, Einstellungsschluessel.AktivitaetenReihenfolge);
        if (wert is not string text)
        {
            return [];
        }

        var ids = new List<long>();
        foreach (string teil in text.Split(Trenner, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(teil, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>
    /// Schreibt die Reihenfolge.
    /// </summary>
    /// <param name="verbindung">Geöffnete Verbindung.</param>
    /// <param name="transaktion">Laufende Transaktion oder <c>null</c>.</param>
    /// <param name="ids">Die Ids der Aktivitäten in der Reihenfolge des Benutzers.</param>
    public static void Speichern(SqliteConnection verbindung, SqliteTransaction? transaktion, IEnumerable<long> ids)
    {
        EinstellungTabelle.Schreiben(verbindung, transaktion, Einstellungsschluessel.AktivitaetenReihenfolge, string.Join(Trenner, ids.Select(id => id.ToString(CultureInfo.InvariantCulture))));
    }
}
