using AndyTours.Application.Import;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.App.ViewModels;

public partial class ApiFitImportViewModel
{
    private const int PruefChunk = 10000;
    public event Action<GarminImportViewModel>? PruefungAngefordert;

    private bool KannPruefen() => Idle() && Dateien.Any(x => x.Uploaded is not null && !x.Abgeschlossen);

    partial void NotifyPruefCommands() => PruefenCommand.NotifyCanExecuteChanged();

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(KannPruefen))]
    private async Task PruefenAsync()
    {
        if (!KannPruefen()) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var chunk = Dateien.Where(x => x.Uploaded is not null && !x.Abgeschlossen).Take(PruefChunk).ToArray();
        Laedt = true;
        FitUploadPruefungAntwort preview;
        try
        {
            preview = await _uploads.Pruefen(new FitUploadPruefanfrage(chunk.Select(x => x.UploadId).ToArray()), linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { return; }
        catch (StammdatenException ex)
        {
            if (IsAlive(linked.Token)) Status = ex.Art == StammdatenFehlerart.Konflikt
                ? "Die Upload-Auswahl hat sich geändert; bitte erneut prüfen."
                : "Die Upload-Prüfung konnte nicht durchgeführt werden.";
            return;
        }
        catch
        {
            if (IsAlive(linked.Token)) Status = "Die Upload-Prüfung konnte nicht durchgeführt werden.";
            return;
        }
        finally
        {
            if (IsAlive(CancellationToken.None)) Laedt = false;
        }

        if (!IsAlive(linked.Token)) return;
        foreach (var error in preview.Fehler)
        {
            var file = chunk.FirstOrDefault(x => x.UploadId == error.UploadId);
            if (file is null) continue;
            file.Abgeschlossen = true;
            file.Status = "FIT ungültig – nicht importiert";
        }
        var rows = preview.Zeilen.Where(row => chunk.Any(f => f.UploadId == row.UploadId)).ToArray();
        if (rows.Length == 0)
        {
            Status = preview.Fehler.Count == 0 ? "Keine gültigen Uploads zur Prüfung." :
                string.Join(" ", preview.Fehler.Select(e => $"{e.Dateiname}: FIT-Datei ungültig."));
            NotifyCommands();
            return;
        }

        var equipment = preview.Equipments.Select(x => new Auswahleintrag(x.Id, x.Name)).ToArray();
        var imports = rows.Select(row =>
        {
            var recording = new Aufzeichnung
            {
                Dateiname = row.Dateiname,
                Geraet = row.Geraet,
                Datum = row.Datum,
                Startzeit = row.Startzeit,
                Fahrzeit = TimeSpan.FromSeconds(row.FahrzeitSekunden),
                DistanzKm = row.DistanzKm,
                Hoehenmeter = row.Hoehenmeter,
                Profil = row.Profil
            };
            var choice = equipment.FirstOrDefault(x => x.Id == row.EmpfohlenesEquipmentId) ?? new Auswahleintrag(null, "(keines)");
            return new Importzeile(recording, row.UploadId.ToString("D"), choice)
            {
                Importieren = !row.BereitsVorhanden,
                Hinweis = $"{row.Dateiname}{(row.BereitsVorhanden ? " – bereits vorhanden, wird übersprungen." : string.Empty)}"
            };
        }).ToArray();

        var errorsText = preview.Fehler.Count == 0 ? string.Empty :
            string.Join(Environment.NewLine, preview.Fehler.Select(e => $"{e.Dateiname}: FIT-Datei ungültig."));
        var zoneText = string.IsNullOrWhiteSpace(preview.FallbackZeitzoneId) ? string.Empty :
            $"Zeitzone für FIT-Dateien ohne Zeitzonenangabe: {preview.FallbackZeitzoneId}.";
        string editorHint = string.Join(Environment.NewLine, new[] { zoneText, errorsText }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var requestId = Guid.NewGuid();
        GarminImportViewModel? editor = null;
        editor = new GarminImportViewModel(imports, equipment, async selected =>
        {
            if (!IsAlive(linked.Token)) return false;
            Aendert = true;
            try
            {
                var selectedBySource = selected.ToDictionary(x => Guid.Parse(x.Quelldatei));
                var choices = rows.Select(row =>
                {
                    var item = selectedBySource[row.UploadId];
                    return new FitUploadAuswahl(row.UploadId, row.DateiVersion, item.Importieren, item.Equipment.Id);
                }).ToArray();
                var result = await _uploads.Uebernehmen(new FitUploadUebernahmeAnfrage(preview.Version, requestId, choices), linked.Token);
                if (!IsAlive(linked.Token)) return false;
                foreach (var response in result.Uploads)
                {
                    var file = Dateien.FirstOrDefault(x => x.UploadId == response.UploadId);
                    if (file is null) continue;
                    file.Uploaded = response;
                    file.Abgeschlossen = true;
                    var outcome = result.Ergebnisse.FirstOrDefault(x => x.UploadId == response.UploadId);
                    file.Status = outcome?.Importiert == true ? "Importiert" :
                        outcome?.Hinweiscode is "tour_bereits_vorhanden" or "bereits_importiert" ? "Bereits vorhanden" : "Übersprungen";
                }
                HatAenderungen = true;
                Status = Dateien.Any(x => x.Uploaded is not null && !x.Abgeschlossen)
                    ? "Auswahl übernommen; weitere Uploads warten auf Prüfung."
                    : "Upload-Auswahl übernommen.";
                return true;
            }
            catch (StammdatenException ex)
            {
                if (!IsAlive(linked.Token)) return false;
                var message = ex.Art == StammdatenFehlerart.Konflikt
                    ? "Der Upload-Bestand wurde geändert. Bitte schließen und erneut prüfen."
                    : "Die Auswahl ist ungültig. Bitte Eingaben prüfen.";
                if (editor is not null) editor.Hinweis = message;
                Status = message;
                return false;
            }
            catch
            {
                if (!IsAlive(linked.Token)) return false;
                HatAenderungen = true;
                var message = "Ergebnis unklar. Es wurde nichts automatisch wiederholt; dieselbe Auswahl kann manuell erneut bestätigt werden.";
                if (editor is not null) editor.Hinweis = message;
                Status = message;
                return false;
            }
            finally
            {
                if (IsAlive(CancellationToken.None)) Aendert = false;
                NotifyCommands();
            }
        });

        editor.ErklaerungOverride = "Angehakte FIT-Dateien werden übernommen. Nicht markierte Dateien bleiben als übersprungen gespeichert; Originaldateien bleiben erhalten.";
        editor.Zusatzhinweis = editorHint;
        if (PruefungAngefordert is null)
        {
            Status = "Prüfungsfenster nicht verfügbar.";
            editor.Dispose();
            return;
        }
        EditorOffen = true;
        try { PruefungAngefordert.Invoke(editor); }
        finally
        {
            if (IsAlive(CancellationToken.None))
            {
                EditorOffen = false;
                NotifyCommands();
            }
            editor.Dispose();
        }
    }
}
