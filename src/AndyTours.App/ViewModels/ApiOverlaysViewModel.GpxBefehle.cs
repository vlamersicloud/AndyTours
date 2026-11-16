using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiOverlaysViewModel
{
    [RelayCommand(CanExecute = nameof(KannGpxImportieren))]
    private async Task GpxImportieren(CancellationToken cancellationToken)
    {
        if (!KannGpxImportieren()) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lebensdauerToken);
        var ct = linked.Token;
        Aendert = true;
        try
        {
            var pending = Importdateien.Where(r => !r.Fertig).ToArray();
            bool manualRetry = pending.FirstOrDefault(r => r.Unklar) is not null;
            if (manualRetry)
            {
                var uncertain = pending.First(r => r.Unklar);
                var outcome = await ImportiereGpxZeileAsync(uncertain, ct);
                ct.ThrowIfCancellationRequested();
                if (_beendet || outcome is GpxImportErgebnis.Unklar or GpxImportErgebnis.BestaetigtOhneFrischenBestand) return;
                if (uncertain.Unklar) return;
                // A retry resolves only this original request; never send other queued files in the same click.
                Hinweis = uncertain.Status;
                return;
            }

            int confirmed = 0, rejected = 0;
            foreach (var row in pending)
            {
                ct.ThrowIfCancellationRequested();
                if (_beendet) return;
                var outcome = await ImportiereGpxZeileAsync(row, ct);
                if (outcome == GpxImportErgebnis.Bestaetigt) confirmed++;
                else if (outcome == GpxImportErgebnis.Abgelehnt) rejected++;
                else
                {
                    Hinweis = $"Importstatus unklar bei „{row.Dateiname}“. Weitere Dateien wurden gestoppt.";
                    return;
                }
            }
            if (!_beendet)
                Hinweis = $"{confirmed} Import(e) bestätigt; {rejected} abgelehnt.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { if (!_beendet) Aendert = false; }
    }

    [RelayCommand(CanExecute = nameof(KannGpxVerwerfen))]
    private void GpxAuswahlVerwerfen()
    {
        if (!KannGpxVerwerfen()) return;
        var pending = Importdateien.Where(r => !r.Fertig && !r.Unklar).ToArray();
        if (pending.Length == 0) return;
        if (!_bestaetigen($"Auswahl von {pending.Length} noch nicht gesendeten Datei(en) verwerfen? Bereits bestätigte oder unklare Importe bleiben erhalten.")) return;
        foreach (var row in pending)
        {
            row.PropertyChanged -= ImportZeileGeaendert;
            Importdateien.Remove(row);
        }
        Hinweis = "Nicht gesendete Dateiauswahl verworfen; Quelldateien bleiben erhalten.";
        OnPropertyChanged(nameof(HatImportdateien));
        BenachrichtigeBefehle();
    }

    private bool KannGpxImportieren() => !_beendet && IstBereit && !IstBeschaeftigt && !_gpxWartetAufReload && Importdateien.Any(r => !r.Fertig);
    private bool KannGpxVerwerfen() => !_beendet && !IstBeschaeftigt && Importdateien.Any(r => !r.Fertig && !r.Unklar);
}
