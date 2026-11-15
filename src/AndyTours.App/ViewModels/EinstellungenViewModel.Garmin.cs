using System.IO;
using AndyTours.Daten;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;

namespace AndyTours.App.ViewModels;

/// <summary>Persönliche Garmin-Einstellungen: maskierte Eingabe, explizites Speichern und Entfernen.</summary>
public partial class EinstellungenViewModel
{
    /// <summary>Alle 20er-Pakete ohne Zwischenfragen herunterladen; gilt sofort für den Datenordner.</summary>
    [ObservableProperty]
    private bool _garminAllePaketeAutomatisch;

    /// <summary>Speichert die Automatikoption sofort und setzt bei einem Fehler die Anzeige zurück.</summary>
    partial void OnGarminAllePaketeAutomatischChanged(bool value)
    {
        if (!_wirdAngezeigt && !SpeichereEinstellung(db => GarminEinstellung.AutomatischSpeichern(db, value)))
        {
            _wirdAngezeigt = true;
            try
            {
                GarminAllePaketeAutomatisch = !value;
            }
            finally
            {
                _wirdAngezeigt = false;
            }
        }
    }

    /// <summary>Nur für das Passwortfeld; kein Property-Binding und keine Anzeige als Klartext.</summary>
    public string GarminSchluessel { get; private set; } = string.Empty;

    /// <summary>Rückmeldung zu geladenem, ungespeichertem oder entferntem Schlüssel.</summary>
    [ObservableProperty]
    private string _garminStatus = string.Empty;

    /// <summary>Übernimmt nur den Entwurf. Anders als beim Routing-Key wird nicht nach jedem Tastendruck gespeichert.</summary>
    public void GarminSchluesselGeaendert(string schluessel)
    {
        if (_wirdAngezeigt || schluessel == GarminSchluessel)
        {
            return;
        }

        GarminSchluessel = schluessel;
        GarminStatus = "Änderung noch nicht gespeichert.";
    }

    /// <summary>Speichert nach ausdrücklicher Bestätigung; leere Eingabe entfernt den Schlüssel.</summary>
    [RelayCommand]
    private void GarminSchluesselSpeichern()
    {
        string wert = GarminSchluessel.Trim();
        // Dasselbe Format wie beim Personal-Client; Fehlermeldungen enthalten nie den eingegebenen Wert.
        if (wert.Any(c => !char.IsAsciiLetterOrDigit(c) && !"-._~+/=".Contains(c)))
        {
            GarminStatus = "Nur den API-Key eingeben, ohne Anführungszeichen, Leerzeichen oder ApiKey-Präfix.";
            return;
        }

        if (SpeichereEinstellung(verbindung => GarminEinstellung.Speichern(verbindung, wert)))
        {
            GarminSchluessel = wert;
            GarminStatus = wert.Length == 0 ? "Kein Garmin-Key hinterlegt." : "Garmin-Key gespeichert. Er wird beim nächsten Import verwendet.";
            SchluesselNeuGelesen?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Entfernt den gespeicherten Schlüssel und leert das maskierte Feld ohne Umgebungsvariablen-Fallback.</summary>
    [RelayCommand]
    private void GarminSchluesselEntfernen()
    {
        if (SpeichereEinstellung(verbindung => GarminEinstellung.Speichern(verbindung, null)))
        {
            GarminSchluessel = string.Empty;
            GarminStatus = "Garmin-Key entfernt.";
            SchluesselNeuGelesen?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Lädt den Key des aktuellen Datenordners; beim Wechsel wird kein alter Entwurf übernommen.</summary>
    private void ZeigeGarmineinstellung()
    {
        _wirdAngezeigt = true;
        try
        {
            using SqliteConnection verbindung = OeffneDatenbank.Ausfuehren(Datenbankdatei);
            GarminSchluessel = GarminEinstellung.Laden(verbindung) ?? string.Empty;
            GarminAllePaketeAutomatisch = GarminEinstellung.AutomatischLaden(verbindung);
            GarminStatus = GarminSchluessel.Length == 0 ? "Kein Garmin-Key hinterlegt." : "Garmin-Key hinterlegt.";
        }
        catch (Exception fehler) when (fehler is SqliteException or IOException or UnauthorizedAccessException)
        {
            GarminSchluessel = string.Empty;
            GarminAllePaketeAutomatisch = false;
            GarminStatus = "Garmin-Einstellung konnte nicht gelesen werden.";
        }
        finally
        {
            _wirdAngezeigt = false;
        }
    }
}
