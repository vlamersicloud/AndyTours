using System.Windows;
using AndyTours.Application.Stammdaten;
using AndyTours.Application.Touren;
using AndyTours.App.ViewModels;
using AndyTours.App.Views;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;

namespace AndyTours.App.Funktionen;

public static partial class ApiTourenMehrfachAnzeige
{
    public static async Task<ApiTourenMehrfachStatus?> Kategorien(
        Window owner,
        IReadOnlyList<TourAntwort> auswahl,
        IStammdatenLeser lookups,
        ITourenMehrfachverwaltung service,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(auswahl);
        ArgumentNullException.ThrowIfNull(lookups);
        ArgumentNullException.ThrowIfNull(service);
        if (auswahl.Count == 0) return null;

        var snapshot = auswahl.Select(t => t with
        {
            Werte = t.Werte with { KategorieIds = t.Werte.KategorieIds.ToArray() }
        }).ToArray();
        var references = Array.AsReadOnly(snapshot.Select(t => new TourVersionsreferenz(t.Id, t.Version)).ToArray());
        var stammdaten = await lookups.Laden(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!owner.IsVisible) return null;

        var bestand = ApiFormularMapper.AlsStammdaten(stammdaten);
        var units = snapshot.Select(ApiFormularMapper.AlsEinheit).ToArray();
        var states = ErmittleKategoriezustaende.Ausfuehren(bestand.Kategorien, units);
        var pickerVm = new KategorienZuordnenViewModel(states, snapshot.Length);
        KategorienZuordnenFenster? picker = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!owner.IsVisible) return null;
            picker = new KategorienZuordnenFenster(pickerVm) { Owner = owner };
            cancellationToken.ThrowIfCancellationRequested();
            if (!owner.IsVisible || picker.ShowDialog() != true || !pickerVm.AendertEtwas)
                return null;

            var request = new TourenKategorienAnfrage(Guid.NewGuid(), references,
                Array.AsReadOnly(pickerVm.Setzen.ToArray()),
                Array.AsReadOnly(pickerVm.Entfernen.ToArray()));
            var names = bestand.Kategorien.Where(k =>
                pickerVm.Setzen.Contains(k.Id) || pickerVm.Entfernen.Contains(k.Id))
                .OrderBy(k => k.Name, StringComparer.CurrentCulture)
                .Select(k => $"{k.Name} ({(pickerVm.Setzen.Contains(k.Id) ? "setzen" : "entfernen")})")
                .ToArray();
            var summary = "Kategorien ändern: " + string.Join(", ", names) + ".";
            var vm = ApiTourenMehrfachViewModel.FuerKategorien(service, request, summary, cancellationToken);
            return Zeige(owner, vm, cancellationToken);
        }
        finally
        {
            if (picker is not null)
            {
                picker.Close();
                picker.DataContext = null;
            }
        }
    }
}
