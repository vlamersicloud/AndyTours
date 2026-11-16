using System.Windows;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Export;
using AndyTours.App.ViewModels;
using AndyTours.App.Views;
using AndyTours.Contracts.V1;

namespace AndyTours.App.Funktionen;

public static class ApiTourdetailAnzeige
{
    public static async Task Oeffnen(Window owner, IReadOnlyList<long> ids, ITourdetailLeser reader,
        ICourseExport export, CancellationToken cancellationToken, ApiOverlayKontext? overlayKontext = null)
    {
        try
        {
            var response = await reader.Laden(new TourdetailAnfrage(ids), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!owner.IsVisible) return;
            if (overlayKontext is not null)
            {
                await overlayKontext.LadenAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!owner.IsVisible) return;
                if (!string.IsNullOrWhiteSpace(overlayKontext.LastHinweis))
                    MessageBox.Show(owner, "Einige Overlays konnten nicht angezeigt werden. Die Tourdetails bleiben verfügbar.", "Overlays",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                if (cancellationToken.IsCancellationRequested || !owner.IsVisible) return;
            }
            if (response.Etappen is null || response.Etappen.Count == 0)
                throw new InvalidOperationException("Empty tour detail response.");

            var stages = ApiTourdetailMapper.AlsEtappen(response);
            if (stages.Count == 1 && stages[0].Verlauf is null)
            {
                MessageBox.Show(owner, "Für diese Tour ist keine lesbare Aufzeichnung vorhanden.", "Tourdetails",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var missing = response.Etappen.Where(s => s.Verlauf is null).ToArray();
            if (missing.Length == 1)
            {
                MessageBox.Show(owner, $"Ohne Aufzeichnung: {missing[0].Tour.Werte.Name}", "Tourdetails",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                if (!owner.IsVisible || cancellationToken.IsCancellationRequested) return;
            }
            else if (missing.Length > 1)
            {
                var names = missing.Take(20).Select(s => s.Tour.Werte.Name).ToArray();
                var summary = string.Join(", ", names);
                if (missing.Length > names.Length) summary += $" und {missing.Length - names.Length} weitere";
                MessageBox.Show(owner, $"Ohne Aufzeichnung: {summary}", "Tourdetails",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                if (!owner.IsVisible || cancellationToken.IsCancellationRequested) return;
            }

            var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var detailToken = lifetime.Token;
            TourdetailFenster? detailWindow = null;
            var lifetimeReleased = false;
            void ReleaseLifetime()
            {
                if (lifetimeReleased) return;
                lifetimeReleased = true;
                lifetime.Cancel();
                lifetime.Dispose();
            }

            try
            {
                var vm = new TourdetailViewModel(stages, ApiGpxOrdner.Laden, ApiGpxOrdner.Speichern,
                    rows => detailWindow?.IsVisible == true
                        ? ApiCourseUebertragung.Zeige(detailWindow, rows, export, detailToken)
                        : Task.CompletedTask);
                detailWindow = new TourdetailFenster(vm, overlayKontext) { Owner = owner };
                detailWindow.Closed += (_, _) => ReleaseLifetime();
                detailWindow.Show();
            }
            catch
            {
                ReleaseLifetime();
                throw;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            if (owner.IsVisible)
                MessageBox.Show(owner, "Die Tourdetails konnten nicht geladen werden.", "Tourdetails",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}