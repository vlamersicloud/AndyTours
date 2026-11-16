using System.Security.Cryptography;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.App.Funktionen;

namespace AndyTours.App.ViewModels;

public partial class ApiGarminImportViewModel
{
    public event Action<GarminImportViewModel>? PruefungAngefordert;
    private bool CanPruefen() => Idle() && Auftrag is { Bereit: > 0 } && Auftrag.Status is not (GarminImportJobStatus.Wartend or GarminImportJobStatus.Laeuft);

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanPruefen))]
    private async Task PruefenAsync()
    {
        if (!CanPruefen() || Auftrag is null) return;
        var captured = Auftrag;
        var token = _lifetime.Token;
        var generation = Interlocked.Increment(ref _generation);
        Laedt = true;
        GarminImportViewModel? editor = null;
        try
        {
            var review = await _pruefung.Laden(captured.JobId, token);
            if (!IsCurrent(generation, token)) return;
            var equipment = review.Equipments.Select(e => new Auswahleintrag(e.Id, e.Name)).ToList();
            var none = new Auswahleintrag(null, GarminImportViewModel.Keines);
            var rows = new List<Importzeile>();
            var versions = new Dictionary<Guid, string>();
            foreach (var row in review.Zeilen)
            {
                var ticks = checked(row.FahrzeitSekunden * TimeSpan.TicksPerSecond);
                var record = new Aufzeichnung { Datum = row.Datum, Startzeit = row.Startzeit, Fahrzeit = TimeSpan.FromTicks(ticks), DistanzKm = row.DistanzKm, Hoehenmeter = row.Hoehenmeter, Profil = row.Profil, Geraet = row.Geraet };
                var selectedEquipment = equipment.FirstOrDefault(e => e.Id == row.EmpfohlenesEquipmentId) ?? none;
                rows.Add(new Importzeile(record, row.KandidatId.ToString("D"), selectedEquipment)
                {
                    Importieren = !row.BereitsVorhanden,
                    Hinweis = row.BereitsVorhanden ? "Tour bereits vorhanden; wird übersprungen." : row.Warncode ?? string.Empty
                });
                versions[row.KandidatId] = row.DateiVersion;
            }
            var requestId = Guid.NewGuid();
            editor = new GarminImportViewModel(rows, equipment, async all =>
            {
                if (!IsCurrent(generation, token)) return false;
                var selection = all.Select(item =>
                {
                    if (!Guid.TryParse(item.Quelldatei, out var id) || !versions.TryGetValue(id, out var version)) throw new InvalidOperationException("Prüfzeile ungültig.");
                    return new GarminImportAuswahl(id, version, item.Importieren, item.Equipment.Id);
                }).ToArray();
                Aendert = true;
                try
                {
                    var response = await _pruefung.Uebernehmen(captured.JobId, new GarminImportUebernahmeAnfrage(review.Version, requestId, selection), token);
                    if (!IsCurrent(generation, token)) return false;
                    HatAenderungen = true;
                    Auftrag = response.Auftrag;
                    Status = "Garmin-Prüfung und Übernahme abgeschlossen.";
                    return true;
                }
                catch (StammdatenException ex)
                {
                    var message = ex.Art switch
                    {
                        StammdatenFehlerart.Konflikt => "Auftrag oder FIT-Datei geändert. Bitte Prüfung aktualisieren.",
                        StammdatenFehlerart.NichtGefunden => "Importauftrag nicht gefunden.",
                        _ => "Übernahmeanfrage ungültig."
                    };
                    if (IsAlive()) { Status = message; if (editor is not null) editor.Hinweis = message; }
                    return false;
                }
                catch
                {
                    if (IsAlive()) { HatAenderungen = true; Status = "Ergebnis unklar; Auftrag möglicherweise verarbeitet. Status aktualisieren, nicht blind erneut bestätigen."; if (editor is not null) editor.Hinweis = Status; }
                    return false;
                }
                finally { if (IsAlive()) Aendert = false; }
            });
            if (!string.IsNullOrWhiteSpace(review.FallbackZeitzoneId))
                editor.Zusatzhinweis = $"Nur falls die FIT-Datei keine Ortszeit enthält: Backend-Fallback-Zeitzone {review.FallbackZeitzoneId}. Bereits gespeicherte Touren werden nicht umgerechnet.";
            if (!IsCurrent(generation, token)) { editor.Dispose(); return; }
            EditorOffen = true;
            Laedt = false;
            try
            {
                if (PruefungAngefordert is null) Status = "Prüfungsfenster derzeit nicht verfügbar.";
                else PruefungAngefordert.Invoke(editor);
            }
            finally
            {
                editor.Dispose();
                if (IsAlive()) EditorOffen = false;
            }
            if (IsAlive()) await RefreshAsync(manual: false, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (StammdatenException ex) { if (IsCurrent(generation, token)) Status = ex.Art == StammdatenFehlerart.Konflikt ? "Prüfung veraltet; bitte aktualisieren." : "FIT-Prüfung konnte nicht geladen werden."; }
        catch { if (IsCurrent(generation, token)) Status = "FIT-Prüfung konnte nicht geladen werden."; }
        finally { if (IsAlive()) { EditorOffen = false; Laedt = false; } editor?.Dispose(); }
    }
}
