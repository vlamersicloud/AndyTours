using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public sealed partial class ApiTourenMehrfachViewModel
{
    public static ApiTourenMehrfachViewModel FuerKategorien(ITourenMehrfachverwaltung service,
        TourenKategorienAnfrage anfrage, CancellationToken lebensdauer = default) =>
        FuerKategorien(service, anfrage, null, lebensdauer);

    public static ApiTourenMehrfachViewModel FuerKategorien(ITourenMehrfachverwaltung service,
        TourenKategorienAnfrage anfrage, string? beschreibung, CancellationToken lebensdauer = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        var normalized = TourenMehrfachRegeln.Normalisieren(anfrage);
        return new ApiTourenMehrfachViewModel("Kategorien für Touren ändern",
            beschreibung ?? "Kategorien der ausgewählten Touren gezielt setzen oder entfernen.",
            normalized.AnfrageId, normalized.Touren.Select(x => x.Id), false,
            ct => service.Kategorien(normalized, ct), lebensdauer);
    }

    public static ApiTourenMehrfachViewModel FuerAktivitaet(ITourenMehrfachverwaltung service,
        TourenAktivitaetAnfrage anfrage, CancellationToken lebensdauer = default) =>
        FuerAktivitaet(service, anfrage, null, lebensdauer);

    public static ApiTourenMehrfachViewModel FuerAktivitaet(ITourenMehrfachverwaltung service,
        TourenAktivitaetAnfrage anfrage, string? beschreibung, CancellationToken lebensdauer = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        var normalized = TourenMehrfachRegeln.Normalisieren(anfrage);
        return new ApiTourenMehrfachViewModel("Aktivität für Touren ändern",
            beschreibung ?? "Aktivität der ausgewählten Touren gemeinsam ändern.",
            normalized.AnfrageId, normalized.Touren.Select(x => x.Id), false,
            ct => service.Aktivitaet(normalized, ct), lebensdauer);
    }

    public static ApiTourenMehrfachViewModel FuerEquipment(ITourenMehrfachverwaltung service,
        TourenEquipmentAnfrage anfrage, CancellationToken lebensdauer = default) =>
        FuerEquipment(service, anfrage, null, lebensdauer);

    public static ApiTourenMehrfachViewModel FuerEquipment(ITourenMehrfachverwaltung service,
        TourenEquipmentAnfrage anfrage, string? beschreibung, CancellationToken lebensdauer = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        var normalized = TourenMehrfachRegeln.Normalisieren(anfrage);
        return new ApiTourenMehrfachViewModel("Equipment für Touren ändern",
            beschreibung ?? "Equipment der ausgewählten Touren gemeinsam setzen oder entfernen.",
            normalized.AnfrageId, normalized.Touren.Select(x => x.Id), false,
            ct => service.Equipment(normalized, ct), lebensdauer);
    }

    public static ApiTourenMehrfachViewModel FuerLoeschen(ITourenMehrfachverwaltung service,
        TourenLoeschenAnfrage anfrage, CancellationToken lebensdauer = default) =>
        FuerLoeschen(service, anfrage, null, lebensdauer);

    public static ApiTourenMehrfachViewModel FuerLoeschen(ITourenMehrfachverwaltung service,
        TourenLoeschenAnfrage anfrage, string? beschreibung, CancellationToken lebensdauer = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        var normalized = TourenMehrfachRegeln.Normalisieren(anfrage);
        return new ApiTourenMehrfachViewModel("Touren löschen",
            beschreibung ?? "Die ausgewählten Touren werden gelöscht. Anhangsdateien bleiben erhalten.",
            normalized.AnfrageId, normalized.Touren.Select(x => x.Id), true,
            ct => service.Loeschen(normalized, ct), lebensdauer);
    }
}
