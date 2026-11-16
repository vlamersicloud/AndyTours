using AndyTours.Application.Import;
using AndyTours.Contracts.V1;
using AndyTours.Import.Dateien;

namespace AndyTours.Import.Dateien;

/// <summary>Server-side FIT upload orchestration; not yet an HTTP or persistence implementation.</summary>
public sealed partial class FitUploadDienst
{
    private readonly IImportUploadSpeicher _speicher;
    private readonly string _datenordner;
    private readonly TimeZoneInfo _fallbackZeitzone;
    private readonly FitDateiDienst _fitDateien;

    public FitUploadDienst(
        IImportUploadSpeicher speicher,
        string datenordner,
        TimeZoneInfo? fallbackZeitzone = null)
    {
        _speicher = speicher ?? throw new ArgumentNullException(nameof(speicher));
        if (string.IsNullOrWhiteSpace(datenordner) || !Path.IsPathFullyQualified(datenordner))
            throw new ArgumentException("Serverdatenordner ungültig.", nameof(datenordner));
        _datenordner = Path.GetFullPath(datenordner);
        _fallbackZeitzone = fallbackZeitzone ?? TimeZoneInfo.Local;
        _fitDateien = new FitDateiDienst(_datenordner, _fallbackZeitzone);
    }

    public Task<FitUploadAntwort?> Laden(Guid uploadId, CancellationToken cancellationToken) =>
        _speicher.Laden(uploadId, cancellationToken);

    public Task<FitUploadListeAntwort> Liste(int start, int anzahl, CancellationToken cancellationToken) =>
        _speicher.Liste(start, anzahl, cancellationToken);
}
