using AndyTours.App.ViewModels;
using System.Windows;
using AndyTours.Application.Planung;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.App.Funktionen;

public static class ApiPlanungsCourseUebertragung
{
    public static void Zeige(Window? owner, Tourenplanung plan, string name, IPlanungsCourseExport port, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(port);
        cancellationToken.ThrowIfCancellationRequested();
        if (owner is { IsVisible: false }) return;
        CourseUebertragenViewModel.ZeigeApi(owner, [new Coursezeile(plan, name)], plan.Profil,
            async (row, _, transferToken) =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, transferToken);
                linked.Token.ThrowIfCancellationRequested();
                if (row.Planung is null) throw new InvalidOperationException("Planung ungültig.");
                var dto = PlanungsVertragMapper.ToDto(row.Planung, linked.Token);
                return await port.Erstellen(new PlanungsGpxExportAnfrage(row.Name, dto), linked.Token).ConfigureAwait(true);
            });
    }
}
