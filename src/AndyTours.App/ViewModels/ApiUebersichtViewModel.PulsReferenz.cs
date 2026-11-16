using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

public partial class ApiUebersichtViewModel
{
    [RelayCommand(CanExecute = nameof(KannPulsLaden))]
    private async Task PulsreferenzLadenAsync(CancellationToken ct)
    {
        if (!KannPulsLaden() || _puls is null) return;
        var generation = _pulsGeneration;
        Laedt = true;
        PulsStatus = "Aktuelle Garmin-Referenz wird vom Backend gelesen …";
        try
        {
            var answer = await _puls.LadenReferenz(ct);
            if (!IstPulsOperationAktuell(generation, ct)) return;
            var previous = Pulsprofil?.Sport;
            UebernimmPulsreferenz(answer, previous);
            LeerePulsanzeige();
            if (previous is not null && Pulsprofil is null)
                PulsStatus = "Das zuvor gewählte Sportprofil ist nicht mehr vorhanden. Bitte bewusst neu wählen.";
            else
                PulsStatus = answer.Abgerufen is null
                    ? "Keine gespeicherte Garmin-Referenz. Ohne Profil werden 10-bpm-Bereiche verwendet."
                    : "Aktuelle Garmin-Referenz geladen; keine historischen Zonen.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (OperationCanceledException)
        {
            if (IstPulsOperationAktuell(generation, ct))
                PulsStatus = "Das Lesen der Pulsreferenz hat das Zeitlimit überschritten.";
        }
        catch (StammdatenException ex) when (ex.Art == StammdatenFehlerart.Konflikt)
        {
            if (IstPulsOperationAktuell(generation, ct))
                PulsStatus = "Die Pulsreferenz wurde geändert. Bitte erneut laden.";
        }
        catch (Exception)
        {
            if (IstPulsOperationAktuell(generation, ct))
                PulsStatus = "Die Pulsreferenz konnte nicht geladen werden.";
        }
        finally
        {
            if (!_pulsBeendet) Laedt = false;
        }
    }

    [RelayCommand(CanExecute = nameof(KannPulsLaden))]
    private async Task GarminZonenAktualisierenAsync(CancellationToken ct)
    {
        if (!KannPulsLaden() || _puls is null) return;
        if (_pulsReferenz is null)
        {
            await PulsreferenzLadenAsync(ct);
            if (!IstPulsOperationAktuell(_pulsGeneration, ct) || _pulsReferenz is null) return;
            PulsStatus = "Referenz wurde offline geladen. Zum ausdrücklichen Garmin-Abruf bitte erneut klicken.";
            return;
        }

        var expected = _pulsReferenz.Version;
        var generation = _pulsGeneration;
        Laedt = true;
        PulsStatus = "Garmin-Referenz wird auf ausdrückliche Anforderung aktualisiert …";
        try
        {
            var answer = await _puls.AktualisierenReferenz(new PulsReferenzAktualisierenAnfrage(expected), ct);
            if (!IstPulsOperationAktuell(generation, ct)) return;
            var previous = Pulsprofil?.Sport;
            UebernimmPulsreferenz(answer, previous);
            LeerePulsanzeige();
            PulsStatus = previous is not null && Pulsprofil is null
                ? "Das zuvor gewählte Sportprofil ist nicht mehr vorhanden. Bitte bewusst neu wählen."
                : "Garmin-Referenz aktualisiert; Zonen sind aktuell, nicht historisch.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (OperationCanceledException)
        {
            _pulsReferenz = null;
            if (IstPulsOperationAktuell(generation, ct))
                PulsStatus = "Der Garmin-Abruf ist unklar fehlgeschlagen. Bitte zuerst die Referenz laden, bevor Sie erneut aktualisieren.";
        }
        catch (StammdatenException ex) when (ex.Art == StammdatenFehlerart.Konflikt)
        {
            if (IstPulsOperationAktuell(generation, ct))
            {
                _pulsReferenz = null;
                PulsStatus = "Die Referenz wurde zwischenzeitlich geändert. Bitte Referenz neu laden.";
            }
        }
        catch (StammdatenException)
        {
            _pulsReferenz = null;
            if (IstPulsOperationAktuell(generation, ct))
                PulsStatus = "Die Garmin-Referenz konnte nicht aktualisiert werden. Bitte zuerst neu laden.";
        }
        catch (Exception)
        {
            _pulsReferenz = null;
            if (IstPulsOperationAktuell(generation, ct))
                PulsStatus = "Der Garmin-Abruf ist unklar fehlgeschlagen. Bitte zuerst die Referenz laden, bevor Sie erneut aktualisieren.";
        }
        finally
        {
            if (!_pulsBeendet) Laedt = false;
        }
    }

    private bool IstPulsOperationAktuell(long generation, CancellationToken ct) =>
        !ct.IsCancellationRequested && !_pulsBeendet && generation == _pulsGeneration;
}
