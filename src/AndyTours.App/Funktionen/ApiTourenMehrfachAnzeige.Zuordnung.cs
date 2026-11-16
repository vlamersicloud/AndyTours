using System.Globalization;
using System.Windows;
using AndyTours.Application.Stammdaten;
using AndyTours.Application.Touren;
using AndyTours.App.ViewModels;
using AndyTours.App.Views;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;

namespace AndyTours.App.Funktionen;

public static partial class ApiTourenMehrfachAnzeige
{
    public static Task<ApiTourenMehrfachStatus?> Aktivitaet(
        Window owner, IReadOnlyList<TourAntwort> auswahl, IStammdatenLeser lookups,
        ITourenMehrfachverwaltung service, CancellationToken cancellationToken) =>
        WaehleZuordnung(owner, auswahl, lookups, service, equipment: false, cancellationToken);

    public static Task<ApiTourenMehrfachStatus?> Equipment(
        Window owner, IReadOnlyList<TourAntwort> auswahl, IStammdatenLeser lookups,
        ITourenMehrfachverwaltung service, CancellationToken cancellationToken) =>
        WaehleZuordnung(owner, auswahl, lookups, service, equipment: true, cancellationToken);

    private static async Task<ApiTourenMehrfachStatus?> WaehleZuordnung(
        Window owner, IReadOnlyList<TourAntwort> auswahl, IStammdatenLeser lookups,
        ITourenMehrfachverwaltung service, bool equipment, CancellationToken cancellationToken)
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
        var activities = snapshot.Select(t => t.Werte.AktivitaetId).Distinct().ToArray();
        var priorValues = snapshot.Select(t => equipment ? t.Werte.EquipmentId : t.Werte.AktivitaetId).Distinct().ToArray();

        var stammdaten = await lookups.Laden(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!owner.IsVisible) return null;
        var bestand = ApiFormularMapper.AlsStammdaten(stammdaten);
        List<Auswahleintrag> entries;
        if (equipment)
        {
            entries = ErmittleEquipmentauswahl.Ausfuehren(bestand, activities);
        }
        else
        {
            entries = bestand.Aktivitaeten.Select(a => new Auswahleintrag(a.Id, a.Name)).ToList();
        }

        var selected = priorValues.Length == 1 ? entries.FirstOrDefault(e => e.Id == priorValues[0]) : null;
        var unitLabel = equipment ? "Neues Equipment" : "Neue Aktivität";
        var title = equipment ? "Equipment für Touren ändern" : "Aktivität für Touren ändern";
        var explanation = snapshot.Length == 1 ? $"{unitLabel}:" :
            $"{unitLabel} für {snapshot.Length.ToString("N0", CultureInfo.GetCultureInfo("de-DE"))} Touren:";
        var pickerVm = new EintragWaehlenViewModel(title, explanation, entries, selected);
        EintragWaehlenFenster? picker = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!owner.IsVisible) return null;
            picker = new EintragWaehlenFenster(pickerVm) { Owner = owner };
            cancellationToken.ThrowIfCancellationRequested();
            if (!owner.IsVisible || picker.ShowDialog() != true || pickerVm.Ergebnis is not { } chosen)
                return null;

            ApiTourenMehrfachViewModel vm;
            string summary;
            if (equipment)
            {
                var name = chosen.Id is null ? "(kein Equipment)" : chosen.Name;
                summary = $"Equipment aller ausgewählten Touren wird auf {name} gesetzt; Kategorien bleiben unverändert.";
                vm = ApiTourenMehrfachViewModel.FuerEquipment(service,
                    new TourenEquipmentAnfrage(Guid.NewGuid(), references, chosen.Id), summary, cancellationToken);
            }
            else if (chosen.Id is { } activityId)
            {
                summary = $"Aktivität aller ausgewählten Touren wird auf „{chosen.Name}“ gesetzt; bestehendes Equipment bleibt unverändert.";
                vm = ApiTourenMehrfachViewModel.FuerAktivitaet(service,
                    new TourenAktivitaetAnfrage(Guid.NewGuid(), references, activityId), summary, cancellationToken);
            }
            else
            {
                return null;
            }
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
