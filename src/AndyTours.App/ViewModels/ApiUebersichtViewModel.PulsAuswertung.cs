using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

public partial class ApiUebersichtViewModel
{
    [RelayCommand(CanExecute = nameof(KannPulsLaden))]
    private async Task PulsdatenLadenAsync(CancellationToken ct)
    {
        if (!KannPulsLaden() || _puls is null) return;
        if (VonDatum is not DateTime vonDate || BisDatum is not DateTime bisDate || vonDate.Date > bisDate.Date)
        {
            PulsStatus = "Bitte einen gültigen Zeitraum auswählen.";
            return;
        }

        var generation = _generation;
        var pulseGeneration = _pulsGeneration;
        var sport = BpmBereiche ? null : Pulsprofil?.Sport;
        var request = new PulsAuswertungsAnfrage(
            DateOnly.FromDateTime(vonDate), DateOnly.FromDateTime(bisDate), Raster,
            AktivitaetAuswahl?.Id, KategorieAuswahl?.Id, sport);
        Laedt = true;
        BenachrichtigePulsCommands();
        PulsStatus = "Pulsmesszeiten werden geladen …";
        try
        {
            var answer = await _puls.Auswerten(request, ct);
            if (ct.IsCancellationRequested || _pulsBeendet ||
                generation != _generation || pulseGeneration != _pulsGeneration) return;

            var previousSport = sport ?? Pulsprofil?.Sport;
            UebernimmPulsreferenz(answer.Referenz, previousSport);
            var display = ApiPulsDarstellung.Erstellen(answer);
            PulsDiagramm = display.Diagramm;
            Pulszeilen = display.Zeilen;
            var reference = answer.VerwendetesProfil is null
                ? "10-bpm-Bereiche ohne Garmin-Zuordnung."
                : $"Aktuelles Referenzprofil {answer.VerwendetesProfil}; keine historischen Zonen.";
            PulsStatus = $"{answer.Aktivitaeten} Aktivitäten; {answer.OhneDaten} ohne auswertbare Pulsintervalle. "
                + $"Gemessene Zeit {TimeSpan.FromSeconds(answer.MesszeitSekunden).TotalHours.ToString("N2", Deutsch)} h; "
                + $"Fahrzeit laut Tourmetadaten {TimeSpan.FromSeconds(answer.FahrzeitSekunden).TotalHours.ToString("N2", Deutsch)} h. "
                + "Pausen, Fehlwerte und Lücken über 60 s werden nicht aufgefüllt; Anteile beziehen sich nur auf gemessene Zeit. "
                + reference;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (OperationCanceledException)
        {
            if (!ct.IsCancellationRequested && !_pulsBeendet)
                PulsStatus = "Die Pulsauswertung hat das Zeitlimit überschritten.";
        }
        catch (StammdatenException ex) when (ex.Art == StammdatenFehlerart.Konflikt)
        {
            if (!ct.IsCancellationRequested && !_pulsBeendet)
            {
                _pulsReferenz = null;
                LeerePulsanzeige();
                PulsStatus = "Pulsreferenz oder Sportprofil wurde geändert. Bitte Referenz neu laden.";
            }
        }
        catch (Exception)
        {
            if (!ct.IsCancellationRequested && !_pulsBeendet)
                PulsStatus = "Die Pulsauswertung konnte nicht geladen werden.";
        }
        finally
        {
            if (!_pulsBeendet)
            {
                Laedt = false;
                BenachrichtigePulsCommands();
            }
        }
    }

    public void BeendePuls()
    {
        if (_pulsBeendet) return;
        _pulsBeendet = true;
        _pulsGeneration++;
        PulsreferenzLadenCommand.Cancel();
        GarminZonenAktualisierenCommand.Cancel();
        PulsdatenLadenCommand.Cancel();
        BenachrichtigePulsCommands();
    }

    private void BenachrichtigePulsCommands()
    {
        PulsreferenzLadenCommand.NotifyCanExecuteChanged();
        GarminZonenAktualisierenCommand.NotifyCanExecuteChanged();
        PulsdatenLadenCommand.NotifyCanExecuteChanged();
    }
}
