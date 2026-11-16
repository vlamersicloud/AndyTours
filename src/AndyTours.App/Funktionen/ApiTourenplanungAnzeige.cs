using System.Windows;
using AndyTours.Application.Planung;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.App.ViewModels;
using AndyTours.App.Views;

namespace AndyTours.App.Funktionen;

public static class ApiTourenplanungAnzeige
{
    public static async Task Oeffnen(Window owner, IPlanungsProfil profilePort, IPlanungsRouting routing,
        IPlanungsDateien files, IPlanungsCourseExport courses, ApiOverlayKontext overlays, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ApiPlanungsProfilSitzung? profile = null;
        ApiTourenplanungSteuerung? controller = null;
        TourenplanungFenster? window = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = await profilePort.Laden(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (owner.IsVisible is false) return;
            if (current is null || current.Version is not { Length: 64 } v || !v.All(Uri.IsHexDigit) || !Enum.IsDefined(current.Profil))
                throw new InvalidOperationException();
            var local = ApiPlanungsAnsichtSpeicher.Laden();
            Geoposition sw = local.SuedWest ?? new Geoposition(47.2, 5.8);
            Geoposition ne = local.NordOst ?? new Geoposition(55.1, 15.1);
            await overlays.LadenAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!owner.IsVisible) return;
            var settings = new Planungseinstellungen(ToCore(current.Profil), sw, ne, local.Ordner);
            var vm = new TourenplanungViewModel(settings, sw, ne,
                () => throw new InvalidOperationException("Routing liegt im Backend."),
                ApiPlanungsAnsichtSpeicher.Speichern);
            vm.ApiRouting = async (profil, von, nach, ct) => await routing.Berechnen(new(ToContract(profil),
                new(new(von.Breite, von.Laenge), null), new(new(nach.Breite, nach.Laenge), null)), ct);
            profile = new ApiPlanungsProfilSitzung(profilePort, current, SynchronizationContext.Current);
            window = new TourenplanungFenster(vm, overlays) { Owner = owner };
            controller = new ApiTourenplanungSteuerung(window, vm, files, courses, profile, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!owner.IsVisible) return;
            window.Show();
            window = null; controller = null; profile = null; // window/controller lifetime owns these resources
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch
        {
            controller?.Dispose();
            profile?.Dispose();
            if (window is not null) { try { window.Close(); } catch { } }
            if (owner.IsVisible) MessageBox.Show(owner, "Die Tourenplanung konnte nicht mit dem Backend geöffnet werden.", "Tour planen", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static CourseFahrprofil ToContract(Fahrprofil p) => p switch { Fahrprofil.Rennrad => CourseFahrprofil.Rennrad, Fahrprofil.Trekking => CourseFahrprofil.Trekking, Fahrprofil.Mountainbike => CourseFahrprofil.Mountainbike, _ => throw new InvalidOperationException() };
    private static Fahrprofil ToCore(CourseFahrprofil p) => p switch { CourseFahrprofil.Rennrad => Fahrprofil.Rennrad, CourseFahrprofil.Trekking => Fahrprofil.Trekking, CourseFahrprofil.Mountainbike => Fahrprofil.Mountainbike, _ => throw new InvalidOperationException() };
}
