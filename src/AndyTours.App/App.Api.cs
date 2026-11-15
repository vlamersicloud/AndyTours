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
            var viewModel = new ApiUebersichtViewModel(sitzung.Auswertung, sitzung.Touren, sitzung.Stammdaten);
            hauptfenster = new ApiHauptfenster(viewModel, sitzung.BasisAdresse);
            var fenster = hauptfenster;
            var uebergebeneSitzung = sitzung;
            var uebergebenesLokalesBackend = lokalesBackend;
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
