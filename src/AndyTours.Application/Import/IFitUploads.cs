using AndyTours.Contracts.V1;

namespace AndyTours.Application.Import;

/// <summary>Application port for opaque FIT uploads; the caller retains ownership of each supplied stream.</summary>
public interface IFitUploads
{
    Task<FitUploadAntwort> Hochladen(
        Guid uploadId,
        string dateiname,
        Stream inhalt,
        CancellationToken cancellationToken);

    Task<FitUploadAntwort?> Laden(
        Guid uploadId,
        CancellationToken cancellationToken);

    Task<FitUploadListeAntwort> Liste(
        int start,
        int anzahl,
        CancellationToken cancellationToken);

    Task<FitUploadPruefungAntwort> Pruefen(
        FitUploadPruefanfrage anfrage,
        CancellationToken cancellationToken);

    Task<FitUploadUebernahmeAntwort> Uebernehmen(
        FitUploadUebernahmeAnfrage anfrage,
        CancellationToken cancellationToken);
}
