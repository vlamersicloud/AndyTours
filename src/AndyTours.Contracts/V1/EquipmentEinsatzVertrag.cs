namespace AndyTours.Contracts.V1;

/// <summary>Eine Anzeigezeile der legacy-kompatiblen Equipment-Einsatz-Auskunft.</summary>
public sealed record EquipmentEinsatzzeileAntwort(
    string Name,
    double? VorherKm,
    double? GesamtKm,
    int? GesamtHm,
    bool IstSumme);

/// <summary>Read-only Zeilen der ungefilterten Equipment-Einsatz-Auskunft.</summary>
public sealed record EquipmentEinsatzAntwort(IReadOnlyList<EquipmentEinsatzzeileAntwort> Zeilen);
