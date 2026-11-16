namespace AndyTours.Contracts.V1;

public enum GarminImportJobStatus { Wartend, Laeuft, PaketBereit, Unterbrochen, Abgebrochen, Fertig }
public enum GarminImportKandidatStatus { Wartend, ZipBereit, FitBereit, Importiert, Uebersprungen, Fehler }
public sealed record GarminImportJobAntwort(
    Guid JobId, string Version, GarminImportJobStatus Status, int Paketnummer, int Bearbeitet, int Bereit,
    int Importiert, bool WeitereMoeglich, bool Automatisch, DateTimeOffset ErstelltUtc,
    DateTimeOffset AktualisiertUtc, IReadOnlyList<string> Warncodes);
public sealed record GarminImportKandidatAntwort(
    Guid KandidatId, long GarminAktivitaetId, GarminImportKandidatStatus Status, DateOnly? Datum,
    string Name, double? DistanzKm, long? FahrzeitSekunden, string? Warncode);
public sealed record GarminImportKandidatenAntwort(Guid JobId, int Start, int Gesamt,
    IReadOnlyList<GarminImportKandidatAntwort> Kandidaten);
public sealed record GarminImportStartAnfrage(Guid AnfrageId);
public sealed record GarminImportSteuerAnfrage(string Version, Guid AnfrageId);
