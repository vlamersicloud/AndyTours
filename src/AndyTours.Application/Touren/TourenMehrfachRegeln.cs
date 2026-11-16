using AndyTours.Contracts.V1;

namespace AndyTours.Application.Touren;

/// <summary>Normalisiert und validiert sichere, unveränderliche Snapshots von Mehrfachanfragen.</summary>
/// <remarks>Diese Regeln führen keine Persistenz, Netzwerk-, Datei- oder Zeitoperationen aus.</remarks>
public static class TourenMehrfachRegeln
{
    private const string Fehlertext = "Mehrfachaktion ist ungültig.";

    /// <summary>Validiert und kopiert eine Kategorienanfrage für die spätere kanonische Quittungsbildung.</summary>
    public static TourenKategorienAnfrage Normalisieren(TourenKategorienAnfrage? anfrage)
    {
        if (anfrage is null || !GuidValide(anfrage.AnfrageId) ||
            !PositiveEindeutigeWerte(anfrage.Setzen, out var setzen) ||
            !PositiveEindeutigeWerte(anfrage.Entfernen, out var entfernen) ||
            setzen.Intersect(entfernen).Any())
            throw Ungueltig();

        return new(anfrage.AnfrageId, Array.AsReadOnly(Referenzen(anfrage.Touren)),
            Array.AsReadOnly(setzen), Array.AsReadOnly(entfernen));
    }

    /// <summary>Validiert und kopiert eine Aktivitätsanfrage.</summary>
    public static TourenAktivitaetAnfrage Normalisieren(TourenAktivitaetAnfrage? anfrage)
    {
        if (anfrage is null || !GuidValide(anfrage.AnfrageId) || anfrage.AktivitaetId <= 0)
            throw Ungueltig();
        return new(anfrage.AnfrageId, Array.AsReadOnly(Referenzen(anfrage.Touren)), anfrage.AktivitaetId);
    }

    /// <summary>Validiert und kopiert eine Equipmentanfrage; null bleibt die ausdrückliche Auswahl „kein“.</summary>
    public static TourenEquipmentAnfrage Normalisieren(TourenEquipmentAnfrage? anfrage)
    {
        if (anfrage is null || !GuidValide(anfrage.AnfrageId) || anfrage.EquipmentId is <= 0)
            throw Ungueltig();
        return new(anfrage.AnfrageId, Array.AsReadOnly(Referenzen(anfrage.Touren)), anfrage.EquipmentId);
    }

    /// <summary>Validiert und kopiert eine Löschanfrage.</summary>
    public static TourenLoeschenAnfrage Normalisieren(TourenLoeschenAnfrage? anfrage)
    {
        if (anfrage is null || !GuidValide(anfrage.AnfrageId))
            throw Ungueltig();
        return new(anfrage.AnfrageId, Array.AsReadOnly(Referenzen(anfrage.Touren)));
    }

    private static TourVersionsreferenz[] Referenzen(IReadOnlyList<TourVersionsreferenz>? touren)
    {
        if (touren is null || touren.Count == 0 || touren.Any(t => t is null || t.Id <= 0 ||
                !VersionValide(t.Version)) || touren.Select(t => t.Id).Distinct().Count() != touren.Count)
            throw Ungueltig();
        return touren.Select(t => new TourVersionsreferenz(t.Id, t.Version.ToUpperInvariant()))
            .OrderBy(t => t.Id).ToArray();
    }

    private static bool PositiveEindeutigeWerte(IReadOnlyList<long>? werte, out long[] normalisiert)
    {
        normalisiert = [];
        if (werte is null || werte.Any(w => w <= 0) || werte.Distinct().Count() != werte.Count)
            return false;
        normalisiert = werte.Order().ToArray();
        return true;
    }

    private static bool GuidValide(Guid id) => id != Guid.Empty;

    private static bool VersionValide(string? version) =>
        version is { Length: 64 } && version.All(c =>
            c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');

    private static UngueltigeTourException Ungueltig() => new(Fehlertext);
}
