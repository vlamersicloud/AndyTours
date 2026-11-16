using System.Globalization;
using System.Windows;
using AndyTours.Application.Touren;
using AndyTours.App.ViewModels;
using AndyTours.App.Views;
using AndyTours.Contracts.V1;

namespace AndyTours.App.Funktionen;

public static partial class ApiTourenMehrfachAnzeige
{
    public static Task<ApiTourenMehrfachStatus?> Loeschen(
        Window owner,
        IReadOnlyList<TourAntwort> auswahl,
        ITourenMehrfachverwaltung service,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(auswahl);
        ArgumentNullException.ThrowIfNull(service);
        if (auswahl.Count == 0) return Task.FromResult<ApiTourenMehrfachStatus?>(null);

        var snapshot = auswahl.Select(t => new TourVersionsreferenz(t.Id, t.Version)).ToArray();
        var request = new TourenLoeschenAnfrage(Guid.NewGuid(), Array.AsReadOnly(snapshot));
        var summary = ErstelleLoeschvorschau(auswahl);
        var vm = ApiTourenMehrfachViewModel.FuerLoeschen(service, request, summary, cancellationToken);
        return Task.FromResult(Zeige(owner, vm, cancellationToken));
    }

    private static ApiTourenMehrfachStatus? Zeige(Window owner, ApiTourenMehrfachViewModel vm,
        CancellationToken cancellationToken)
    {
        ApiTourenMehrfachFenster? window = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!owner.IsVisible) return null;
            window = new ApiTourenMehrfachFenster(vm) { Owner = owner };
            cancellationToken.ThrowIfCancellationRequested();
            if (!owner.IsVisible) return null;
            window.ShowDialog();
            return vm.Status;
        }
        finally
        {
            vm.Dispose();
            if (window is not null)
            {
                window.Dispose();
                if (window.IsVisible) window.Close();
            }
        }
    }

    private static string ErstelleLoeschvorschau(IReadOnlyList<TourAntwort> touren)
    {
        const string anhangHinweis = "Anhangsdateien bleiben erhalten.";
        if (touren.Count == 1)
        {
            var tour = touren[0].Werte;
            return $"„{tour.Name}“ vom {tour.Datum.ToString("d. MMMM yyyy", CultureInfo.GetCultureInfo("de-DE"))} wird gelöscht. {anhangHinweis}";
        }
        return $"{touren.Count.ToString("N0", CultureInfo.GetCultureInfo("de-DE"))} ausgewählte Touren werden gelöscht. {anhangHinweis}";
    }
}
