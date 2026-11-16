using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.Application.Garmin;

/// <summary>Serverinterner Bestand; darf niemals als HTTP-Antwort serialisiert oder vom Client geliefert werden.</summary>
public sealed record GarminImportPruefbestand(
    GarminImportJobAntwort Auftrag,
    Datenbestand Bestand,
    IReadOnlyList<GarminImportKandidatAntwort> Kandidaten);

/// <summary>Serverinterne FIT-Vorbereitung samt relativem Artefaktpfad.</summary>
public sealed record GarminImportVorbereitung(
    Guid KandidatId,
    Aufzeichnung Aufzeichnung,
    string RelativerPfad);

/// <summary>Serverinterne Speichergrenze für Garmin-Prüfung und atomare Übernahme.</summary>
public interface IGarminImportPruefspeicher
{
    Task<GarminImportPruefbestand> Pruefbestand(Guid jobId, CancellationToken cancellationToken);

    Task<GarminImportUebernahmeAntwort?> Quittung(
        Guid jobId,
        GarminImportUebernahmeAnfrage anfrage,
        CancellationToken cancellationToken);

    Task<GarminImportUebernahmeAntwort> Uebernehmen(
        Guid jobId,
        GarminImportUebernahmeAnfrage anfrage,
        IReadOnlyList<GarminImportVorbereitung> vorbereitet,
        CancellationToken cancellationToken);
}
