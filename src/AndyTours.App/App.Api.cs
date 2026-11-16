using System.IO;
using System.Windows;
using AndyTours.App.Funktionen;
using AndyTours.App.ViewModels;
using AndyTours.App.Views;
using AndyTours.Client;

namespace AndyTours.App;

public partial class App
{
    private async Task StarteApiModusAsync()
    {
        BackendClientSitzung? sitzung = null;
        LokalesBackend? lokalesBackend = null;
        ApiHauptfenster? hauptfenster = null;
        ApiOverlayVerwaltungHost? overlayHost = null;
        try
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            using var dialog = new BackendVerbindungFenster(ErmittleApiProgrammPfad());
            if (dialog.ShowDialog() != true || dialog.Sitzung is null)
            {
                Shutdown();
                return;
            }

            sitzung = dialog.Sitzung;
            lokalesBackend = dialog.LokalesBackend;
            var viewModel = new ApiUebersichtViewModel(sitzung.Auswertung, sitzung.Touren, sitzung.Stammdaten, sitzung.Puls);
            hauptfenster = new ApiHauptfenster(viewModel, sitzung.BasisAdresse);
            var fenster = hauptfenster;
            var uebergebeneSitzung = sitzung;
            var uebergebenesLokalesBackend = lokalesBackend;
            overlayHost = new ApiOverlayVerwaltungHost(fenster, uebergebeneSitzung.OverlayLeser,
                uebergebeneSitzung.OverlayVerwaltung, uebergebeneSitzung.OverlayGpxImport);
            fenster.OverlaysAngefordert += overlayHost.Oeffnen;
            fenster.TourenplanungAngefordert += ct => ApiTourenplanungAnzeige.Oeffnen(fenster,
                uebergebeneSitzung.PlanungsProfil, uebergebeneSitzung.PlanungsRouting, uebergebeneSitzung.PlanungsDateien,
                uebergebeneSitzung.PlanungsCourseExport, overlayHost.Kontext, ct, uebergebeneSitzung.PlanungsStartgebiet);
            fenster.AktivitaetenVerwaltenAngefordert += async () =>
            {
                ApiAktivitaetenFenster? verwaltungsfenster = null;
                try
                {
                    var verwaltungsViewModel = new ApiAktivitaetenViewModel(uebergebeneSitzung.Aktivitaeten,
                        text => MessageBox.Show((Window?)verwaltungsfenster ?? fenster, text, "Aktivität bestätigen",
                            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);
                    verwaltungsfenster = new ApiAktivitaetenFenster(verwaltungsViewModel) { Owner = fenster };
                    verwaltungsfenster.ShowDialog();
                }
                catch (Exception)
                {
                    viewModel.StatusText = "Die Aktivitätenverwaltung konnte nicht geöffnet werden.";
                }
                finally
                {
                    if (verwaltungsfenster?.IsVisible == true)
                    {
                        try { verwaltungsfenster.Close(); }
                        catch (Exception) { }
                    }
                    if (fenster.IsVisible)
                    {
                        try { await viewModel.AktualisierenAsync(CancellationToken.None); }
                        catch (Exception) { viewModel.StatusText = "Übersicht konnte nach der Verwaltung nicht aktualisiert werden."; }
                    }
                }
            };
            fenster.KategorienVerwaltenAngefordert += async () =>
            {
                ApiKategorienFenster? verwaltungsfenster = null;
                try
                {
                    var verwaltungsViewModel = new ApiKategorienViewModel(uebergebeneSitzung.Kategorien,
                        text => MessageBox.Show((Window?)verwaltungsfenster ?? fenster, text, "Kategorie bestätigen",
                            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);
                    verwaltungsfenster = new ApiKategorienFenster(verwaltungsViewModel) { Owner = fenster };
                    verwaltungsfenster.ShowDialog();
                }
                catch (Exception)
                {
                    viewModel.StatusText = "Die Kategorienverwaltung konnte nicht geöffnet werden.";
                }
                finally
                {
                    if (verwaltungsfenster?.IsVisible == true)
                    {
                        try { verwaltungsfenster.Close(); }
                        catch (Exception) { }
                    }
                    if (fenster.IsVisible)
                    {
                        try { await viewModel.AktualisierenAsync(CancellationToken.None); }
                        catch (Exception) { viewModel.StatusText = "Übersicht konnte nach der Kategorienverwaltung nicht aktualisiert werden."; }
                    }
                }
            };

            fenster.EquipmentsVerwaltenAngefordert += async () =>
            {
                ApiEquipmentsFenster? admin = null;
                try
                {
                    var vm = new ApiEquipmentsViewModel(uebergebeneSitzung.Equipments, uebergebeneSitzung.Stammdaten,
                        text => MessageBox.Show((Window?)admin ?? fenster, text, "Equipment bestätigen",
                            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);
                    admin = new ApiEquipmentsFenster(vm) { Owner = fenster };
                    admin.ShowDialog();
                }
                catch (Exception) { viewModel.StatusText = "Die Equipmentverwaltung konnte nicht geöffnet werden."; }
                finally
                {
                    admin?.Dispose();
                    if (fenster.IsVisible)
                    {
                        try { await viewModel.AktualisierenAsync(CancellationToken.None); }
                        catch (Exception) { viewModel.StatusText = "Übersicht konnte nach der Equipmentverwaltung nicht aktualisiert werden."; }
                    }
                }
            };

            fenster.FaelligeAlarmeLadenAngefordert += async ct =>
                (await uebergebeneSitzung.Wartung.Laden(new(null, null), ct)).FaelligkeitsHinweis;

            fenster.WartungVerwaltenAngefordert += async () =>
            {
                ApiWartungsFenster? admin = null;
                try
                {
                    admin = new ApiWartungsFenster(new ApiWartungsViewModel(uebergebeneSitzung.Wartung,
                        uebergebeneSitzung.Stammdaten,
                        text => MessageBox.Show((Window?)admin ?? fenster, text, "Wartungen bestätigen",
                            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes))
                    { Owner = fenster };
                    admin.ShowDialog();
                }
                finally { admin?.Dispose(); }
            };

            fenster.TextlisteVerwaltenAngefordert += async art =>
            {
                ApiTextlisteFenster? admin = null;
                try
                {
                    var vm = new ApiTextlisteViewModel(art, uebergebeneSitzung.Textlisten,
                        text => MessageBox.Show((Window?)admin ?? fenster, text, "Textlisteneintrag bestätigen",
                            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);
                    admin = new ApiTextlisteFenster(vm) { Owner = fenster };
                    admin.ShowDialog();
                }
                finally { admin?.Dispose(); }
            };

            fenster.SaisonzieleVerwaltenAngefordert += async () =>
            {
                var snapshot = await uebergebeneSitzung.Saisonziele.Laden(CancellationToken.None);
                var aktivitaeten = snapshot.Aktivitaeten.Select(a => new AndyTours.Core.Modell.Aktivitaet
                {
                    Id = a.Id, Name = a.Name, IstHaupt = a.IstHaupt, Geschwindigkeitseinheit = a.Geschwindigkeitseinheit,
                    ZielKm = a.ZielKm, ZielHm = a.ZielHm, ZielAnzahl = a.ZielAnzahl
                }).ToList();
                var ziele = snapshot.Ziele.Select(z => new AndyTours.Core.Modell.Saisonziel
                {
                    AktivitaetId = z.AktivitaetId, AbJahr = z.AbJahr, ZielKm = z.ZielKm, ZielHm = z.ZielHm, ZielAnzahl = z.ZielAnzahl
                }).ToList();
                SaisonzieleViewModel? editor = null;
                editor = new SaisonzieleViewModel(aktivitaeten, ziele, DateTime.Today.Year,
                    async (jahre, geaenderteZiele) =>
                    {
                        var request = new AndyTours.Contracts.V1.SaisonzieleSpeichernAnfrage(snapshot.Version, jahre.ToArray(),
                            geaenderteZiele.Select(z => new AndyTours.Contracts.V1.SaisonzielAntwort(z.AktivitaetId, z.AbJahr, z.ZielKm, z.ZielHm, z.ZielAnzahl)).ToList());
                        try
                        {
                            await uebergebeneSitzung.Saisonziele.Speichern(request, CancellationToken.None);
                            return true;
                        }
                        catch (AndyTours.Application.Stammdaten.StammdatenException ex)
                        {
                            editor!.Hinweis = ex.Art == AndyTours.Application.Stammdaten.StammdatenFehlerart.Konflikt
                                ? ex.Message + " Eingaben bleiben erhalten; bitte schließen und neu laden."
                                : ex.Message;
                            return false;
                        }
                        catch (Exception)
                        {
                            editor!.Hinweis = "Speicherstatus unklar. Bitte Bestand neu laden und prüfen; nicht blind erneut speichern.";
                            return false;
                        }
                    });
                var admin = new SaisonzieleFenster(editor) { Owner = fenster };
                admin.ShowDialog();
            };

            fenster.TourdetailAngefordert += (ids, ct) =>
                ApiTourdetailAnzeige.Oeffnen(fenster, ids, uebergebeneSitzung.Tourdetail,
                    uebergebeneSitzung.CourseExport, ct, overlayHost?.Kontext);

            fenster.GarminGeraetImportierenAngefordert += () =>
            {
                var vm = new ApiFitImportViewModel(uebergebeneSitzung.FitUploads);
                var dialog = new ApiFitImportFenster(vm, geraeteBeimStartLesen: true) { Owner = fenster };
                try { dialog.ShowDialog(); }
                finally { dialog.Dispose(); }
                return Task.CompletedTask;
            };

            fenster.FitDateienImportierenAngefordert += () =>
            {
                var vm = new ApiFitImportViewModel(uebergebeneSitzung.FitUploads);
                var dialog = new ApiFitImportFenster(vm) { Owner = fenster };
                try { dialog.ShowDialog(); }
                finally { dialog.Dispose(); }
                return Task.CompletedTask;
            };

            fenster.GarminImportAngefordert += () =>
            {
                var vm = new ApiGarminImportViewModel(uebergebeneSitzung.GarminImportJobs, uebergebeneSitzung.GarminImportPruefung);
                var dialog = new ApiGarminImportFenster(vm) { Owner = fenster };
                try { dialog.ShowDialog(); }
                finally { dialog.Dispose(); }
                return Task.CompletedTask;
            };

            fenster.GarminEinstellungenAngefordert += () =>
            {
                var vm = new ApiGarminEinstellungenViewModel(uebergebeneSitzung.GarminEinstellungen);
                var dialog = new ApiGarminEinstellungenFenster(vm) { Owner = fenster };
                try { dialog.ShowDialog(); }
                finally { dialog.Dispose(); }
                return Task.CompletedTask;
            };

            fenster.ProfilBearbeitenAngefordert += async () =>
            {
                try
                {
                    var snapshot = await uebergebeneSitzung.Profil.Laden(CancellationToken.None);
                    var driver = snapshot.Fahrer is null ? new AndyTours.Core.Modell.Fahrer() : new AndyTours.Core.Modell.Fahrer
                    {
                        Id = snapshot.Fahrer.Id, Vorname = snapshot.Fahrer.Vorname, Nachname = snapshot.Fahrer.Nachname,
                        Geburtstag = snapshot.Fahrer.Geburtstag, GroesseCm = snapshot.Fahrer.GroesseCm,
                        GewichtKg = snapshot.Fahrer.GewichtKg, Ruhepuls = snapshot.Fahrer.Ruhepuls,
                        Maxpuls = snapshot.Fahrer.Maxpuls, Heimatort = snapshot.Fahrer.Heimatort
                    };
                    var input = AndyTours.Core.Berechnungen.FahrerEingaben.AlsEingaben(driver);
                    PersoenlicheDatenViewModel? editor = null;
                    editor = new PersoenlicheDatenViewModel(input,
                        values => AndyTours.Core.Berechnungen.FahrerEingaben.AlsFahrer(values, driver, DateOnly.FromDateTime(DateTime.Today)).Hinweis,
                        async values =>
                        {
                            var (updated, error) = AndyTours.Core.Berechnungen.FahrerEingaben.AlsFahrer(values, driver, DateOnly.FromDateTime(DateTime.Today));
                            if (updated is null) { editor!.Hinweis = error ?? "Eingaben ungültig."; return false; }
                            try
                            {
                                var valuesDto = new AndyTours.Contracts.V1.ProfilWerte(updated.Vorname, updated.Nachname, updated.Geburtstag,
                                    updated.GroesseCm, updated.GewichtKg, updated.Ruhepuls, updated.Maxpuls, updated.Heimatort);
                                await uebergebeneSitzung.Profil.Speichern(new AndyTours.Contracts.V1.ProfilSpeichernAnfrage(snapshot.Version, valuesDto), CancellationToken.None);
                                return true;
                            }
                            catch (AndyTours.Application.Stammdaten.StammdatenException ex)
                            {
                                editor!.Hinweis = ex.Art == AndyTours.Application.Stammdaten.StammdatenFehlerart.Konflikt
                                    ? ex.Message + " Eingaben bleiben erhalten; bitte neu laden."
                                    : ex.Message;
                                return false;
                            }
                            catch (Exception)
                            {
                                editor!.Hinweis = "Speicherstatus unklar. Bitte Bestand neu laden und prüfen; nicht blind erneut speichern.";
                                return false;
                            }
                        });
                    var dialog = new PersoenlicheDatenFenster(editor!) { Owner = fenster };
                    dialog.ShowDialog();
                }
                catch (AndyTours.Application.Stammdaten.StammdatenException ex) { viewModel.StatusText = ex.Message; }
                catch (Exception) { viewModel.StatusText = "Profil konnte nicht geladen werden. Bitte Verbindung prüfen."; }
            };

            fenster.Closed += async (_, _) =>
            {
                var bereinigungFehlgeschlagen = false;
                try
                {
                    fenster.Dispose();
                    if (uebergebenesLokalesBackend is not null)
                        await uebergebenesLokalesBackend.DisposeAsync();
                    else
                        uebergebeneSitzung.Dispose();
                }
                catch
                {
                    bereinigungFehlgeschlagen = true;
                }

                if (bereinigungFehlgeschlagen)
                {
                    MessageBox.Show("Die Backendverbindung oder der lokale Backendprozess konnte nicht vollständig beendet werden.",
                        "AndyTours", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                Shutdown();
            };

            MainWindow = fenster;
            // Expliziter Shutdown bleibt aktiv, bis der Closed-Handler die eigenen Ressourcen freigegeben hat.
            fenster.Show();
            sitzung = null;
            lokalesBackend = null;
            hauptfenster = null;
        }
        catch
        {
            overlayHost?.Dispose();
            if (hauptfenster is not null) hauptfenster.Dispose();
            var bereinigungFehlgeschlagen = false;
            try
            {
                if (lokalesBackend is not null)
                    await lokalesBackend.DisposeAsync();
                else
                    sitzung?.Dispose();
            }
            catch { bereinigungFehlgeschlagen = true; }

            var meldung = bereinigungFehlgeschlagen
                ? "Der API-Modus konnte nicht gestartet werden; eigene Backendressourcen konnten nicht vollständig beendet werden."
                : "Der API-Modus konnte nicht gestartet werden. Bitte Backendverbindung und Installation prüfen.";
            MessageBox.Show(meldung, "AndyTours", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static string ErmittleApiProgrammPfad()
    {
        var installiert = Path.Combine(AppContext.BaseDirectory, "backend", "AndyTours.Api.exe");
        if (File.Exists(installiert)) return installiert;
        installiert = Path.Combine(AppContext.BaseDirectory, "backend", "AndyTours.Api.dll");
        if (File.Exists(installiert)) return installiert;

#if DEBUG
        var konfigurationen = new[] { "Debug", "Release" };
#else
        var konfigurationen = new[] { "Release", "Debug" };
#endif
        for (var ordner = new DirectoryInfo(AppContext.BaseDirectory); ordner is not null; ordner = ordner.Parent)
        {
            if (!File.Exists(Path.Combine(ordner.FullName, "AndyTours.slnx"))) continue;
            foreach (var konfiguration in konfigurationen)
            {
                var quellcode = Path.Combine(ordner.FullName, "src", "AndyTours.Api", "bin", konfiguration, "net10.0", "AndyTours.Api.dll");
                if (File.Exists(quellcode)) return quellcode;
            }
            break;
        }

        // Zentralbetrieb benötigt keine lokale Backenddatei; lokales Starten weist den fehlenden Pfad ab.
        return installiert;
    }
}
