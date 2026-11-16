using System.Net.Http;
using System.Windows;
using AndyTours.Application.Touren;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using AndyTours.Client;
using AndyTours.App.Views;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndyTours.App.ViewModels;

public partial class ApiUebersichtViewModel
{
    [ObservableProperty] private bool _editorOffen;

    [RelayCommand(CanExecute = nameof(KannNeueTour))]
    private async Task NeueTourAsync(CancellationToken ct)
    {
        if (Laedt) return;
        var gespeichert = false;
        Laedt = true;
        try
        {
            var stammdaten = await _stammdatenLeser.Laden(ct);
            var bestand = ApiFormularMapper.AlsStammdaten(stammdaten);
            if (bestand.Aktivitaeten.Count == 0)
            {
                StatusText = "Keine Aktivitäten vorhanden. Stammdatenverwaltung ist noch nicht verfügbar.";
                return;
            }

            var bevorzugteAktivitaetId = AktivitaetAuswahl?.Id;
            if (bevorzugteAktivitaetId is { } gewaehlt &&
                !bestand.Aktivitaeten.Any(a => a.Id == gewaehlt))
                bevorzugteAktivitaetId = null;
            var einheit = ErstelleNeueEinheit.Ausfuehren(bestand, bevorzugteAktivitaetId, DateOnly.FromDateTime(DateTime.Today));
            var anfrageId = Guid.NewGuid();
            gespeichert = ZeigeEditor(einheit, bestand, stammdaten.Trainingsbereiche, istNeu: true,
                async (werte, fenster) =>
                {
                    try
                    {
                        await _touren.AnlegenIdempotent(ApiFormularMapper.AlsWerte(werte), anfrageId, ct);
                        return true;
                    }
                    catch (Exception fehler)
                    {
                        ZeigeSpeicherfehler(fenster, fehler);
                        return false;
                    }
                }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            StatusText = "Erstellen der Tour abgebrochen.";
        }
        catch (Exception)
        {
            StatusText = "Stammdaten konnten nicht geladen werden. Bitte Verbindung prüfen.";
        }
        finally
        {
            Laedt = false;
        }

        if (gespeichert)
        {
            await AktualisierenAsync(ct);
            StatusText = "Tour erstellt.";
        }
    }

    [RelayCommand(CanExecute = nameof(KannMitAuswahl))]
    private async Task TourBearbeitenAsync(CancellationToken ct)
    {
        if (Laedt || AusgewaehlteTour is null) return;
        await BearbeiteAuswahlAsync(kopieren: false, ct);
    }

    [RelayCommand(CanExecute = nameof(KannMitAuswahl))]
    private async Task TourKopierenAsync(CancellationToken ct)
    {
        if (Laedt || AusgewaehlteTour is null) return;
        await BearbeiteAuswahlAsync(kopieren: true, ct);
    }

    private async Task BearbeiteAuswahlAsync(bool kopieren, CancellationToken ct)
    {
        var auswahl = AusgewaehlteTour;
        if (auswahl is null || Laedt) return;
        var gespeichert = false;
        Laedt = true;
        try
        {
            var stammdaten = await _stammdatenLeser.Laden(ct);
            var frisch = await _touren.Lesen(auswahl.Id, ct);
            if (frisch is null)
            {
                StatusText = "Diese Tour ist nicht mehr vorhanden. Bitte Liste aktualisieren.";
                return;
            }

            var bestand = ApiFormularMapper.AlsStammdaten(stammdaten);
            if (bestand.Aktivitaeten.Count == 0)
            {
                StatusText = "Keine Aktivitäten vorhanden. Stammdatenverwaltung ist noch nicht verfügbar.";
                return;
            }

            var original = ApiFormularMapper.AlsEinheit(frisch);
            var einheit = kopieren
                ? KopiereEinheit.Ausfuehren(original, DateOnly.FromDateTime(DateTime.Today))
                : original;
            var version = frisch.Version;
            var anfrageId = kopieren ? Guid.NewGuid() : Guid.Empty;
            gespeichert = ZeigeEditor(einheit, bestand, stammdaten.Trainingsbereiche, istNeu: kopieren,
                async (werte, fenster) =>
                {
                    try
                    {
                        if (kopieren)
                            await _touren.AnlegenIdempotent(ApiFormularMapper.AlsWerte(werte), anfrageId, ct);
                        else
                            await _touren.Aendern(frisch.Id,
                                new TourAendernAnfrage(version, ApiFormularMapper.AlsWerte(werte)), ct);
                        return true;
                    }
                    catch (TourVersionskonfliktException)
                    {
                        StatusText = "Versionskonflikt: Die Tour wurde zwischenzeitlich geändert. Bitte Liste aktualisieren, bevor erneut gespeichert wird.";
                        MessageBox.Show(fenster, StatusText, "Tour nicht gespeichert",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    catch (TourNichtGefundenException)
                    {
                        StatusText = "Diese Tour ist nicht mehr vorhanden. Bitte Liste aktualisieren.";
                        MessageBox.Show(fenster, StatusText, "Tour nicht gespeichert",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    catch (Exception fehler)
                    {
                        ZeigeSpeicherfehler(fenster, fehler);
                        return false;
                    }
                }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            StatusText = "Bearbeitung der Tour abgebrochen.";
        }
        catch (Exception)
        {
            StatusText = "Tour oder Stammdaten konnten nicht geladen werden. Bitte Liste und Verbindung prüfen.";
        }
        finally
        {
            Laedt = false;
        }

        if (gespeichert)
        {
            await AktualisierenAsync(ct);
            StatusText = kopieren ? "Tour kopiert." : "Tour gespeichert.";
        }
    }

    [RelayCommand(CanExecute = nameof(KannMitAuswahl))]
    private async Task TourLoeschenAsync(CancellationToken ct)
    {
        var auswahl = AusgewaehlteTour;
        if (Laedt || auswahl is null) return;
        var bestaetigung = MessageBox.Show(
            $"Tour „{auswahl.Werte.Name}“ wirklich löschen?", "Tour löschen",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (bestaetigung != MessageBoxResult.Yes || Laedt) return;

        var geloescht = false;
        Laedt = true;
        try
        {
            await _touren.Loeschen(auswahl.Id, auswahl.Version, ct);
            geloescht = true;
        }
        catch (TourVersionskonfliktException)
        {
            StatusText = "Versionskonflikt: Die Tour wurde zwischenzeitlich geändert. Bitte Liste aktualisieren.";
        }
        catch (TourNichtGefundenException)
        {
            StatusText = "Diese Tour ist nicht mehr vorhanden. Bitte Liste aktualisieren.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            StatusText = "Löschstatus ist unklar. Bitte zuerst neu laden, nicht erneut löschen.";
        }
        catch (Exception)
        {
            StatusText = "Löschstatus ist unklar. Bitte zuerst neu laden, nicht erneut löschen.";
        }
        finally
        {
            Laedt = false;
        }

        if (geloescht)
        {
            await AktualisierenAsync(ct);
            StatusText = "Tour gelöscht.";
        }
    }

    private bool KannNeueTour() => !Laedt;
    private bool KannMitAuswahl() => !Laedt && !EditorOffen && AuswahlAnzahl == 1 && AusgewaehlteTour is not null;

    partial void OnAusgewaehlteTourChanged(TourAntwort? value) => BenachrichtigeTourCommands();

    private void BenachrichtigeTourCommands()
    {
        OnPropertyChanged(nameof(HatTourenauswahl));
        NeueTourCommand.NotifyCanExecuteChanged();
        TourBearbeitenCommand.NotifyCanExecuteChanged();
        TourKopierenCommand.NotifyCanExecuteChanged();
        TourLoeschenCommand.NotifyCanExecuteChanged();
    }

    private bool ZeigeEditor(
        Trainingseinheit einheit,
        Datenbestand bestand,
        IReadOnlyList<string> trainingsbereiche,
        bool istNeu,
        Func<Trainingseinheit, Window, Task<bool>> speichern,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EditorOffen = true;
        try
        {
            var listen = ErmittleEingabelisten.Ausfuehren(bestand, einheit, trainingsbereiche);
            EinheitBearbeitenFenster? fenster = null;
            var editorViewModel = new EinheitBearbeitenViewModel(
                einheit, listen, bestand.Kategorien, istNeu ? "Neue Tour" : "Tour bearbeiten",
                werte => speichern(werte, fenster!), bestand, istNeu);
            ct.ThrowIfCancellationRequested();
            fenster = new EinheitBearbeitenFenster(editorViewModel) { Owner = System.Windows.Application.Current.MainWindow };
            fenster.ShowDialog();
            ct.ThrowIfCancellationRequested();
            return editorViewModel.IstGespeichert;
        }
        finally
        {
            EditorOffen = false;
        }
    }

    private void ZeigeSpeicherfehler(Window fenster, Exception fehler)
    {
        var meldung = fehler switch
        {
            BackendApiException api when (int)api.StatusCode >= 500 =>
                "Speicherstatus ist unklar. Bitte zuerst neu laden, nicht erneut anlegen.",
            HttpRequestException or OperationCanceledException =>
                "Speicherstatus ist unklar. Bitte zuerst neu laden, nicht erneut anlegen.",
            UngueltigeTourException =>
                "Das Backend hat die Tourdaten abgelehnt. Bitte Eingaben prüfen.",
            _ => "Speicherstatus konnte nicht bestätigt werden. Bitte zuerst neu laden, nicht erneut anlegen."
        };
        StatusText = meldung;
        MessageBox.Show(fenster, meldung, "Tour nicht gespeichert",
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
