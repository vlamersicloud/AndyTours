namespace AndyTours.Contracts.V1;

/// <summary>Versionierter Equipmentbestand.</summary>
public sealed record EquipmentsAntwort(string Version, IReadOnlyList<EquipmentAntwort> Eintraege);

/// <summary>Bearbeitbare Equipmentwerte.</summary>
public sealed record EquipmentWerte(
    string Name,
    long? AktivitaetId,
    string Geraetezuordnung,
    string StandardKategorie,
    bool IstStandard,
    bool Ausgemustert,
    DateOnly? Anschaffungsdatum,
    double AnfangsdistanzKm);

/// <summary>Anfrage zum Anlegen oder Ändern von Equipment.</summary>
public sealed record EquipmentSpeichernAnfrage(string Version, EquipmentWerte Werte);
