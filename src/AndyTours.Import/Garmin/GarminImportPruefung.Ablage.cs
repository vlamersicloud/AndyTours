using System.Globalization;
using AndyTours.Import.Dateien;

namespace AndyTours.Import.Garmin;

public sealed partial class GarminImportPruefung
{
    private Task<string> LegeSnapshotAb(Guid kandidatId, long garminId, FitDateiSnapshot fit, CancellationToken cancellationToken)
    {
        if (kandidatId == Guid.Empty || garminId <= 0)
            throw new AndyTours.Application.Stammdaten.StammdatenException(
                AndyTours.Application.Stammdaten.StammdatenFehlerart.Ungueltig, "FIT-Ablage ungültig.");
        var relative = FormattableString.Invariant($"{fit.Aufzeichnung.Datum.Year:D4}/garmin-{garminId.ToString(CultureInfo.InvariantCulture)}-{kandidatId:N}.fit");
        return _fitDateien.UnveraenderlichAblegen(relative, fit, cancellationToken);
    }
}
