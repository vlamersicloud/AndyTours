using System.Windows;
using AndyTours.Application.Auswertung;
using AndyTours.App.ViewModels;
using AndyTours.App.Views;

namespace AndyTours.App.Funktionen;

/// <summary>Öffnet die serverseitige Equipment-Einsatzanzeige als besitzgebundenes Dialogfenster.</summary>
public static class ApiEquipmentEinsatzAnzeige
{
    public static async Task Oeffnen(Window owner, IEquipmentEinsatz service, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(service);
        ct.ThrowIfCancellationRequested();
        if (!owner.IsVisible) return;

        var response = await service.Laden(ct);
        ct.ThrowIfCancellationRequested();
        if (!owner.IsVisible) return;

        var viewModel = new ApiEquipmentEinsatzViewModel(response);
        ApiEquipmentEinsatzFenster? window = null;
        try
        {
            window = new ApiEquipmentEinsatzFenster(viewModel) { Owner = owner };
            ct.ThrowIfCancellationRequested();
            if (!owner.IsVisible) return;
            window.ShowDialog();
        }
        finally
        {
            window?.Dispose();
        }
    }
}
