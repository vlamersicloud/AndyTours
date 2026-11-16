using System.IO;
using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiOverlaysViewModel
{
    private enum GpxImportErgebnis { Bestaetigt, Abgelehnt, Unklar, BestaetigtOhneFrischenBestand }

    private bool _gpxWartetAufReload;
    public bool GpxWartetAufReload => _gpxWartetAufReload;

    private async Task<GpxImportErgebnis> ImportiereGpxZeileAsync(ApiOverlayGpxDatei row, CancellationToken cancellationToken)
    {
        if (_beendet || !Importdateien.Contains(row)) return GpxImportErgebnis.Abgelehnt;
        bool warUnklar = row.Unklar;
        var bestand = BestehenderBestand;
        OverlayGpxImportAnfrage request;
        try
        {
            if (row.Anfrage is { } exactRetry) request = exactRetry;
            else
            {
                if (bestand is null || (row.OrdnerId is { } target && !bestand.Ordner.Any(f => f.Id == target)))
                {
                    row.Status = "Zielordner nicht mehr vorhanden";
                    row.Fertig = true;
                    return GpxImportErgebnis.Abgelehnt;
                }
                request = OverlayGpxRegeln.Normalisieren(new OverlayGpxImportAnfrage(
                    bestand.Version, row.AnfrageId, row.OrdnerId, row.Dateiname));
                row.Anfrage = request;
            }
        }
        catch (StammdatenException)
        {
            row.Status = "Ungültige Importdaten";
            row.Fertig = true;
            return GpxImportErgebnis.Abgelehnt;
        }

        if (_beendet || cancellationToken.IsCancellationRequested) return warUnklar ? GpxImportErgebnis.Unklar : GpxImportErgebnis.Abgelehnt;
        row.Status = "Import wird gesendet";
        bool gesendet = false;
        try
        {
            await using var stream = new FileStream(row.Pfad, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 81920, options: FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length > OverlayGpxRegeln.MaxDateiBytes)
            {
                row.Status = "Datei ist zu groß";
                row.Fertig = true;
                return GpxImportErgebnis.Abgelehnt;
            }
            if (!stream.CanRead) throw new IOException();
            gesendet = true;
            var result = await _gpxImport.Importieren(request, stream, cancellationToken);
            if (_beendet) return GpxImportErgebnis.Unklar;
            if (result is null || result.Id <= 0 || result.Bestand is null)
            {
                row.Status = "Importantwort ungültig; Status bitte prüfen";
                row.Unklar = true;
                return GpxImportErgebnis.Unklar;
            }
            row.Status = "Import bestätigt; Bestand wird aktualisiert";
            row.Fertig = true;
            row.Unklar = false;
            _gpxWartetAufReload = true;
            OnPropertyChanged(nameof(GpxWartetAufReload));
            var fresh = await AktualisiereNachSchreibenAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (_beendet) return GpxImportErgebnis.BestaetigtOhneFrischenBestand;
            if (fresh is null)
            {
                _gpxWartetAufReload = true;
                row.Status = "Import bestätigt; bitte Bestand neu laden";
                return GpxImportErgebnis.BestaetigtOhneFrischenBestand;
            }
            _gpxWartetAufReload = false;
            OnPropertyChanged(nameof(GpxWartetAufReload));
            bool exists = fresh.Overlays.Any(o => o.Id == result.Id);
            row.Status = exists ? "Importiert" : "Bestätigt; nicht mehr im aktuellen Bestand";
            return GpxImportErgebnis.Bestaetigt;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!_beendet)
            {
                if (row.Fertig) { row.Unklar = false; row.Status = "Import bestätigt; Bestand bitte neu laden"; _gpxWartetAufReload = true; OnPropertyChanged(nameof(GpxWartetAufReload)); }
                else { row.Unklar = gesendet || warUnklar; row.Status = row.Unklar ? "Importstatus unklar; dieselbe Anfrage erneut prüfen" : "Import abgebrochen"; if (!row.Unklar) row.Fertig = true; }
            }
            return row.Unklar ? GpxImportErgebnis.Unklar : GpxImportErgebnis.Abgelehnt;
        }
        catch (StammdatenException ex) when (ex.Art is StammdatenFehlerart.Ungueltig or StammdatenFehlerart.NichtGefunden or StammdatenFehlerart.Konflikt)
        {
            if (_beendet) return warUnklar ? GpxImportErgebnis.Unklar : GpxImportErgebnis.Abgelehnt;
            if (warUnklar)
            {
                row.Unklar = true;
                row.Status = "Vorheriger Importstatus unklar; zuerst Bestand prüfen";
                return GpxImportErgebnis.Unklar;
            }
            row.Fertig = true;
            row.Status = ex.Art == StammdatenFehlerart.Konflikt ? "Bestand geändert; bitte neu laden" : "Import abgelehnt";
            return GpxImportErgebnis.Abgelehnt;
        }
        catch (OverlayGpxZuGrossException)
        {
            if (!_beendet && !warUnklar) { row.Status = "Datei ist zu groß"; row.Fertig = true; }
            else if (!_beendet) row.Status = "Vorheriger Importstatus unklar; bitte Bestand prüfen";
            return warUnklar ? GpxImportErgebnis.Unklar : GpxImportErgebnis.Abgelehnt;
        }
        catch (Exception ex) when (!gesendet && !warUnklar && ex is IOException or UnauthorizedAccessException or FileNotFoundException)
        {
            if (!_beendet) { row.Fertig = true; row.Unklar = false; row.Status = "Quelldatei kann nicht gelesen werden"; }
            return GpxImportErgebnis.Abgelehnt;
        }
        catch (Exception)
        {
            if (!_beendet)
            {
                row.Unklar = true;
                row.Status = "Importstatus unklar; dieselbe Anfrage erneut prüfen";
            }
            return GpxImportErgebnis.Unklar;
        }
    }
}
