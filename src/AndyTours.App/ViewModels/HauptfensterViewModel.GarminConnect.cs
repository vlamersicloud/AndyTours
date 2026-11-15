using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using AndyTours.Daten;
using AndyTours.Daten.Datenordner;
using AndyTours.Import.Garmin;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;

namespace AndyTours.App.ViewModels;

/// <summary>Live-Import eigener Radaktivitäten über Garmins offizielle, lesende Personal API.</summary>
public partial class HauptfensterViewModel
{
    // Nur während des modalen Imports belegt; die dauerhafte Zuordnung steht in der Datenbank.
    private readonly Dictionary<string, long> _garminConnectIds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Sammelt Warnungen synchron im sequenziellen Importlauf, ohne verzögerte UI-Callbacks.</summary>
    private sealed class DirekteImportwarnung(List<string> warnungen) : IProgress<string>
    {
        public void Report(string value) => warnungen.Add(value);
    }

    /// <summary>Lädt neue Connect-Aktivitäten und verwendet anschließend die vorhandene Importauswahl samt Equipment.</summary>
    [RelayCommand]
    private void VonGarminConnectImportieren()
    {
        Statusmeldung = string.Empty;
        if (!PruefeHauptaktivitaet())
        {
            return;
        }

        string? schluessel = Environment.GetEnvironmentVariable("GARMIN_PERSONAL_API_KEY");
        if (string.IsNullOrWhiteSpace(schluessel))
        {
            MessageBox.Show("Bitte einen Personal API-Key auf https://connect.garmin.com/app/settings/personalApiToken erstellen "
                + "und die Umgebungsvariable GARMIN_PERSONAL_API_KEY setzen. Danach AndyTours neu starten.",
                Importtitel, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string zwischenordner = Path.Combine(Path.GetTempPath(), "AndyTours_Connect_" + Guid.NewGuid().ToString("N"));
        try
        {
            HashSet<long> ids;
            using (SqliteConnection verbindung = OeffneDatenbank.Ausfuehren(Datenbankdatei))
            {
                ids = GarminImportZuordnung.Laden(verbindung, _bestand.Trainingseinheiten.Select(e => e.Id).ToHashSet());
            }

            List<AndyTours.Core.Modell.Trainingseinheit> vorhanden = [.. _bestand.Trainingseinheiten];
            string archivordner = Path.Combine(Datenordner, "GarminConnect");
            Leseergebnis ergebnis = LeseMitFortschritt("Garmin Connect: Radaktivitäten werden geladen …", fortschritt =>
            {
                // Der vorhandene Fortschrittsdialog führt diese Arbeit auf einem Hintergrundthread aus.
                // Redirects sind bewusst aus: Zugangsdaten dürfen den offiziellen API-Host nicht verlassen.
                using var handler = new HttpClientHandler { AllowAutoRedirect = false };
                using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
                using var zeitlimit = new CancellationTokenSource(TimeSpan.FromMinutes(30));
                var client = new GarminPersonalClient(http, schluessel);
                var meldung = new Progress<(int Aktuell, int Gesamt)>(p =>
                    fortschritt.Report(new KopierFortschritt(p.Aktuell, p.Gesamt, 0, 0)));
                var gelesen = new Leseergebnis(true, [], []);
                var warnungen = new DirekteImportwarnung(gelesen.Uebersprungen);
                IReadOnlyList<(long Id, string Pfad)> dateien = LadeGarminConnectAufzeichnungen.AusfuehrenAsync(
                    client, archivordner, zwischenordner, ids, meldung, warnungen, zeitlimit.Token).GetAwaiter().GetResult();
                foreach ((long id, string pfad) in dateien)
                {
                    _garminConnectIds.Add(pfad, id);
                    Lies(pfad, vorhanden, gelesen);
                }

                return gelesen;
            });

            Importiere(ergebnis);
        }
        catch (Exception fehler) when (fehler is HttpRequestException or IOException or UnauthorizedAccessException
            or OperationCanceledException or JsonException or InvalidOperationException or SqliteException or InvalidDataException)
        {
            // Schlüssel/Token und Response-Bodies werden nicht ausgegeben.
            string hinweis = fehler is OperationCanceledException
                ? "Zeitlimit erreicht. Bitte erneut versuchen; fertige Downloads bleiben erhalten."
                : fehler.Message;
            MessageBox.Show("Garmin Connect konnte nicht importiert werden:\n\n" + hinweis,
                Importtitel, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _garminConnectIds.Clear();
            LoescheZwischenordner(zwischenordner);
        }
    }
}
