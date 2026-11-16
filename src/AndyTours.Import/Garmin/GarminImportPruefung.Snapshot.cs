using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Import.Dateien;

namespace AndyTours.Import.Garmin;

public sealed partial class GarminImportPruefung
{
    private readonly IGarminImportPruefspeicher _speicher;
    private readonly string _datenordner;
    private readonly FitDateiDienst _fitDateien;
    private readonly TimeZoneInfo _fallbackZeitzone;

    public GarminImportPruefung(IGarminImportPruefspeicher speicher, string datenordner, TimeZoneInfo? fallbackZeitzone = null)
    {
        _speicher = speicher ?? throw new ArgumentNullException(nameof(speicher));
        if (string.IsNullOrWhiteSpace(datenordner) || !Path.IsPathFullyQualified(datenordner))
            throw new ArgumentException("Serverdatenordner ungültig.", nameof(datenordner));
        _datenordner = Path.GetFullPath(datenordner);
        _fallbackZeitzone = fallbackZeitzone ?? TimeZoneInfo.Local;
        _fitDateien = new FitDateiDienst(_datenordner, _fallbackZeitzone);
    }

    private Task<FitDateiSnapshot> LeseSnapshot(Guid jobId, Guid kandidatId, CancellationToken cancellationToken)
    {
        if (jobId == Guid.Empty || kandidatId == Guid.Empty)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Datei ungültig.");
        var directory = Path.Combine(_datenordner, "GarminImport", jobId.ToString("N"));
        var source = Path.Combine(directory, kandidatId.ToString("N") + ".fit");
        return _fitDateien.Lesen(source, directory, cancellationToken);
    }
}
