using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.Application.Import;

/// <summary>Server-interner Prüfbestand; enthält Core-Daten und darf nie als HTTP-Antwort serialisiert werden.</summary>
public sealed record FitUploadPruefbestand(
    string Version,
    Datenbestand Bestand,
    IReadOnlyList<FitUploadAntwort> Uploads);

/// <summary>Server-interne FIT-Vorbereitung; der relative Pfad wird ausschließlich vom Server erzeugt.</summary>
public sealed record FitUploadVorbereitung(
    Guid UploadId,
    Aufzeichnung Aufzeichnung,
    string RelativerPfad);

/// <summary>
/// Server-only metadata and commit port for uploads in this backend's single authenticated dataset.
/// It does not bind ownership to transient access tokens and performs no file or network I/O.
/// </summary>
public interface IImportUploadSpeicher
{
    Task<FitUploadAntwort> Registrieren(
        Guid uploadId,
        string dateiname,
        string inhaltHash,
        long byteanzahl,
        CancellationToken cancellationToken);

    Task<FitUploadAntwort?> Laden(
        Guid uploadId,
        CancellationToken cancellationToken);

    Task<FitUploadListeAntwort> Liste(
        int start,
        int anzahl,
        CancellationToken cancellationToken);

    Task<FitUploadPruefbestand> Pruefbestand(
        IReadOnlyList<Guid> uploadIds,
        CancellationToken cancellationToken);

    Task<FitUploadUebernahmeAntwort?> Quittung(
        FitUploadUebernahmeAnfrage anfrage,
        CancellationToken cancellationToken);

    Task<FitUploadUebernahmeAntwort> Uebernehmen(
        FitUploadUebernahmeAnfrage anfrage,
        IReadOnlyList<FitUploadVorbereitung> vorbereitet,
        CancellationToken cancellationToken);
}
