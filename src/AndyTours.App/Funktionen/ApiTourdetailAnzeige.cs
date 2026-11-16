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
        ICourseExport export, CancellationToken cancellationToken)
    {
        try
        {
            var response = await reader.Laden(new TourdetailAnfrage(ids), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!owner.IsVisible) return;
            if (response.Etappen is null || response.Etappen.Count == 0)
                throw new InvalidOperationException("Empty tour detail response.");

            var stages = ApiTourdetailMapper.AlsEtappen(response);
            if (stages.Count == 1 && stages[0].Verlauf is null)
            {
                MessageBox.Show(owner, TourdetailViewModel.KeineAufzeichnung, "Tourdetails",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var missing = response.Etappen.Where(s => s.Verlauf is null).ToArray();
            if (missing.Length > 0)
            {
                var names = missing.Take(20).Select(s => s.Tour.Werte.Name).ToArray();
                var summary = string.Join(", ", names);
                if (missing.Length > names.Length) summary += $" und {missing.Length - names.Length} weitere";
                MessageBox.Show(owner, $"Ohne Aufzeichnung: {summary}", "Tourdetails",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                if (!owner.IsVisible || cancellationToken.IsCancellationRequested) return;
            }

            var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var vm = new TourdetailViewModel(stages, ApiGpxOrdner.Laden, ApiGpxOrdner.Speichern,
                rows => ApiCourseUebertragung.Zeige(owner, rows, export, lifetime.Token));
            var window = new TourdetailFenster(vm) { Owner = owner };
            window.Closed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
            window.Show();
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