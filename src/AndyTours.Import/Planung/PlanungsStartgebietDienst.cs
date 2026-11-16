using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Import.Dateien;

namespace AndyTours.Import.Planung;

public sealed class PlanungsStartgebietDienst : IPlanungsStartgebiet
{
    private const int MaxDateien = 5;
    private readonly IPlanungsStartQuelldaten _quelldaten;
    private readonly FitDateiDienst _fit;

    public PlanungsStartgebietDienst(IPlanungsStartQuelldaten quelldaten, string datenordner)
    {
        _quelldaten = quelldaten ?? throw new ArgumentNullException(nameof(quelldaten));
        if (string.IsNullOrWhiteSpace(datenordner) || !Path.IsPathFullyQualified(datenordner))
            throw new ArgumentException("Ein absoluter Datenordner ist erforderlich.", nameof(datenordner));
        _fit = new FitDateiDienst(Path.GetFullPath(datenordner), TimeZoneInfo.Utc);
    }

    public async Task<PlanungsStartgebietAntwort> Laden(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pfade = await _quelldaten.Laden(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        int versucht = 0;
        for (int i = 0; i < pfade.Count && versucht < MaxDateien; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pfad = pfade[i];
            if (!_fit.VerlaufExistiert(pfad)) continue;
            versucht++;
            try
            {
                using var verlauf = await _fit.VerlaufLesen(pfad, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var strecke = ErmittleStrecke.Ausfuehren(verlauf.Punkte);
                if (strecke is null || !Gueltig(strecke.SuedWest) || !Gueltig(strecke.NordOst) ||
                    strecke.SuedWest.Breite > strecke.NordOst.Breite || strecke.SuedWest.Laenge > strecke.NordOst.Laenge)
                    continue;
                return new PlanungsStartgebietAntwort(
                    new PlanungsPosition(strecke.SuedWest.Breite, strecke.SuedWest.Laenge),
                    new PlanungsPosition(strecke.NordOst.Breite, strecke.NordOst.Laenge));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (StammdatenException) { }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new PlanungsStartgebietAntwort(null, null);
    }

    private static bool Gueltig(AndyTours.Core.Modell.Geoposition p) =>
        double.IsFinite(p.Breite) && p.Breite is >= -90 and <= 90 &&
        double.IsFinite(p.Laenge) && p.Laenge is >= -180 and <= 180;
}
