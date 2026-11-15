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

        string zwischenordner = Path.Combine(Path.GetTempPath(), "AndyTours_Connect_" + Guid.NewGuid().ToString("N"));
        try
        {
            string? schluessel;
            bool automatisch;
            using (SqliteConnection verbindung = OeffneDatenbank.Ausfuehren(Datenbankdatei))
            {
                schluessel = GarminEinstellung.Laden(verbindung);
                automatisch = GarminEinstellung.AutomatischLaden(verbindung);
            }

            if (string.IsNullOrWhiteSpace(schluessel))
            {
                MessageBox.Show("Bitte unter „Extras → Einstellungen → Persönlich → Garmin Connect“ "
                    + "den Personal API-Key eingeben und „Key speichern“ wählen.",
                    Importtitel, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            HashSet<long> ids;
            using (SqliteConnection verbindung = OeffneDatenbank.Ausfuehren(Datenbankdatei))
            {
                ids = GarminImportZuordnung.Laden(verbindung, _bestand.Trainingseinheiten.Select(e => e.Id).ToHashSet());
            }

            string archivordner = Path.Combine(Datenordner, "GarminConnect");
            // Client und Iterator bleiben über die kleinen Pakete hinweg erhalten.
            // Dadurch wird weder jedes Mal neu angemeldet noch die Liste von vorne abgerufen.
            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
            var client = new GarminPersonalClient(http, schluessel);
            IAsyncEnumerator<long> aktivitaeten = client.LeseRadaktivitaetsIdsAsync().GetAsyncEnumerator();
            try
            {
                var gesammelt = new Leseergebnis(true, [], []);
                List<AndyTours.Core.Modell.Trainingseinheit> vorhanden = [.. _bestand.Trainingseinheiten];
                while (true)
                {
                    if (!automatisch)
                    {
                        _garminConnectIds.Clear();
                        vorhanden = [.. _bestand.Trainingseinheiten];
                    }
                    GarminDownloadPaket? paket = null;
                    Leseergebnis ergebnis = LeseMitFortschritt("Garmin Connect: Paket mit höchstens 20 Aktivitäten …", fortschritt =>
                    {
                        var meldung = new Progress<(int Aktuell, int Gesamt)>(p =>
                            fortschritt.Report(new KopierFortschritt(p.Aktuell, p.Gesamt, 0, 0)));
                        var gelesen = new Leseergebnis(true, [], []);
                        var warnungen = new DirekteImportwarnung(gelesen.Uebersprungen);
                        paket = LadeGarminConnectAufzeichnungen.LadePaketAsync(
                            client, aktivitaeten, archivordner, zwischenordner, ids, meldung, warnungen).GetAwaiter().GetResult();
                        foreach ((long id, string pfad) in paket.Dateien)
                        {
                            _garminConnectIds.Add(pfad, id);
                            Lies(pfad, vorhanden, gelesen);
                        }

                        return gelesen;
                    });

                    if (automatisch)
                    {
                        // Der gemeinsame Duplikatbestand wird beim FIT-Lesen fortgeschrieben.
                        // Temporäre Dateien und ID-Zuordnungen bis zur einmaligen Importauswahl behalten.
                        gesammelt.Neue.AddRange(ergebnis.Neue);
                        gesammelt.Uebersprungen.AddRange(ergebnis.Uebersprungen);
                        if (paket!.Unterbrochen || !paket.WeitereMoeglich)
                        {
                            Importiere(gesammelt);
                            break;
                        }

                        continue;
                    }

                    // Auch bei Netzwerkabbruch die fertigen Dateien dieses Pakets anbieten.
                    // Abbrechen der Importauswahl oder ein Datenbankfehler beendet die Sitzung.
                    bool fortsetzen = true;
                    if (ergebnis.Neue.Count > 0 || ergebnis.Uebersprungen.Count > 0 || !paket!.WeitereMoeglich)
                    {
                        fortsetzen = Importiere(ergebnis);
                    }

                    LoescheZwischenordner(zwischenordner);
                    if (!fortsetzen || paket!.Unterbrochen || !paket.WeitereMoeglich)
                    {
                        break;
                    }

                    if (MessageBox.Show("Dieses Paket ist verarbeitet. Weitere höchstens 20 Aktivitäten laden?\n\n"
                        + "Bereits importierte Touren bleiben gespeichert. Mit „Nein“ kannst du später fortsetzen.",
                        Importtitel, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                    {
                        break;
                    }
                }
            }
            finally
            {
                aktivitaeten.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
        catch (Exception fehler) when (fehler is HttpRequestException or IOException or UnauthorizedAccessException
            or OperationCanceledException or JsonException or InvalidOperationException or SqliteException or InvalidDataException)
        {
            // Schlüssel/Token und Response-Bodies werden nicht ausgegeben.
            string hinweis = fehler is OperationCanceledException
                ? "Zeitlimit beim Download erreicht. Bitte erneut versuchen; fertige Downloads bleiben erhalten."
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
