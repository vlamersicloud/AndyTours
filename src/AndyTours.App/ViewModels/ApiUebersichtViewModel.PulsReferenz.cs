using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

public partial class ApiUebersichtViewModel
{
    [RelayCommand(CanExecute = nameof(KannPulsLaden))]
    private async Task PulsreferenzLadenAsync()
    {
        if (!KannPulsLaden() || _puls is null) return;
        var generation = _pulsGeneration;
        Laedt = true;
        PulsStatus = "Aktuelle Garmin-Referenz wird vom Backend gelesen …";
        try
        {
            var answer = await _puls.LadenReferenz(CancellationToken.None);
            if (generation != _pulsGeneration || _pulsBeendet) return;
            var previous = Pulsprofil?.Sport;
            UebernimmPulsreferenz(answer, previous);
            LeerePulsanzeige();
            PulsStatus = answer.Abgerufen is null
                ? "Keine gespeicherte Garmin-Referenz. Ohne Profil werden 10-bpm-Bereiche verwendet."
                : "Aktuelle Garmin-Referenz geladen; keine historischen Zonen.";
        }
        catch (OperationCanceledException) { }
        catch (StammdatenException ex) when (ex.Art == StammdatenFehlerart.Konflikt)
        {
            if (generation == _pulsGeneration && !_pulsBeendet)
                PulsStatus = "Die Pulsreferenz wurde geändert. Bitte erneut laden.";
        }
        catch (Exception)
        {
            if (generation == _pulsGeneration && !_pulsBeendet)
                PulsStatus = "Die Pulsreferenz konnte nicht geladen werden.";
        }
        finally
        {
            if (!_pulsBeendet) Laedt = false;
        }
    }

    [RelayCommand(CanExecute = nameof(KannPulsLaden))]
    private async Task GarminZonenAktualisierenAsync()
    {
        if (!KannPulsLaden() || _puls is null) return;
        if (_pulsReferenz is null)
        {
            await PulsreferenzLadenAsync();
            if (_pulsReferenz is null || _pulsBeendet) return;
            PulsStatus = "Referenz wurde offline geladen. Zum ausdrücklichen Garmin-Abruf bitte erneut klicken.";
            return;
        }

        var expected = _pulsReferenz.Version;
        var generation = _pulsGeneration;
        Laedt = true;
        PulsStatus = "Garmin-Referenz wird auf ausdrückliche Anforderung aktualisiert …";
        try
        {
            var answer = await _puls.AktualisierenReferenz(
                new PulsReferenzAktualisierenAnfrage(expected), CancellationToken.None);
            if (generation != _pulsGeneration || _pulsBeendet) return;
            UebernimmPulsreferenz(answer, Pulsprofil?.Sport);
            LeerePulsanzeige();
            PulsStatus = "Garmin-Referenz aktualisiert; Zonen sind aktuell, nicht historisch.";
        }
        catch (OperationCanceledException) { }
        catch (StammdatenException ex) when (ex.Art == StammdatenFehlerart.Konflikt)
        {
            if (generation == _pulsGeneration && !_pulsBeendet)
                PulsStatus = "Die Referenz wurde zwischenzeitlich geändert. Bitte Referenz neu laden.";
        }
        catch (StammdatenException)
        {
            if (generation == _pulsGeneration && !_pulsBeendet)
                PulsStatus = "Garmin-Key fehlt oder die Referenzanfrage ist ungültig.";
        }
        catch (Exception)
        {
            if (generation == _pulsGeneration && !_pulsBeendet)
                PulsStatus = "Der Garmin-Abruf ist unklar fehlgeschlagen. Bitte zuerst die Referenz laden, bevor Sie erneut aktualisieren.";
        }
        finally
        {
            if (!_pulsBeendet) Laedt = false;
        }
    }
}
