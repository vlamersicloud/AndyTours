namespace AndyTours.Contracts.V1;

public sealed record GarminImportPruefzeile(Guid KandidatId, long GarminAktivitaetId, string DateiVersion, DateOnly Datum, TimeOnly Startzeit, long FahrzeitSekunden, double DistanzKm, int Hoehenmeter, string Geraet, string Profil, long? EmpfohlenesEquipmentId, bool BereitsVorhanden, string? Warncode);
public sealed record GarminImportPruefungAntwort(Guid JobId, string Version, IReadOnlyList<GarminImportPruefzeile> Zeilen, IReadOnlyList<EquipmentAntwort> Equipments);
public sealed record GarminImportAuswahl(Guid KandidatId, string DateiVersion, bool Uebernehmen, long? EquipmentId);
public sealed record GarminImportUebernahmeAnfrage(string Version, Guid AnfrageId, IReadOnlyList<GarminImportAuswahl> Auswahl);
public sealed record GarminImportUebernahmeZeile(Guid KandidatId, long? TourId, bool Importiert, string? Hinweiscode);
public sealed record GarminImportUebernahmeAntwort(GarminImportJobAntwort Auftrag, IReadOnlyList<GarminImportUebernahmeZeile> Ergebnisse);
