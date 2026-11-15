using System.Text.Json;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Speichert nur Pulszonengrenzen und Abrufdatum im jeweiligen Datenordner; keinen API-Key oder Token.</summary>
public static class GarminPulszonenEinstellung
{
    private const string Schluessel = "Persoenlich.Garmin.PulszonenReferenz";
    /// <summary>Ungültige Altwerte werden nicht als Zonenreferenz verwendet.</summary>
    public static GarminPulszonenStand? Laden(SqliteConnection verbindung)
    {
        string? json = EinstellungTabelle.Lesen(verbindung, Schluessel);
        if (json is null) { return null; }
        try
        {
            GarminPulszonenStand? stand = JsonSerializer.Deserialize<GarminPulszonenStand>(json);
            if (stand?.Profile is null || stand.Profile.Any(p => p is null || p.Untergrenzen is null
                || p.Untergrenzen.Count != 5 || p.Untergrenzen.Any(g => g <= 0 || g >= 255)
                || p.Untergrenzen.Zip(p.Untergrenzen.Skip(1)).Any(pair => pair.First >= pair.Second)))
            { return null; }
            return stand;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Die gespeicherten Gesundheits-/Profilwerte wandern mit Datenbestand und Sicherungen mit.</summary>
    public static void Speichern(SqliteConnection verbindung, GarminPulszonenStand stand) =>
        EinstellungTabelle.Schreiben(verbindung, null, Schluessel, JsonSerializer.Serialize(stand));
}
