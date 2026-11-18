namespace AndyTours.Application.Sicherung;

using AndyTours.Application.Koordination;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;

/// <summary>
/// Runs once before the API host starts listening. Holds one outer exclusive dataset lease for the full
/// count/check/archive/reset sequence and never retries. External writers are not coordinated.
/// </summary>
public sealed class BackendStartsicherungsKoordinator(
    IDatenbestandskoordination datenbestand,
    IBackendStartsicherungsZaehler zaehler,
    IBackendStartsicherungsArchiv archiv)
{
    public async Task<BackendStartsicherungsErgebnis> Ausfuehren(CancellationToken cancellationToken)
    {
        var vorgangsId = Guid.NewGuid();
        var counterAdvanced = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var lease = await datenbestand.ExklusivBetreten(cancellationToken).ConfigureAwait(false);
            var stand = await zaehler.ZaehlenUndErhoehen(cancellationToken).ConfigureAwait(false);
            counterAdvanced = true;

            if (stand.Intervall is < 1 or > 99 || stand.StartsSeitSicherung < 0 || stand.Trainingseinheiten < 0)
                return new(BackendStartsicherungsStatus.NichtVerfuegbar);
            if (!IstSicherungFaellig.Ausfuehren(stand.StartsSeitSicherung, stand.Intervall, stand.Trainingseinheiten))
                return new(BackendStartsicherungsStatus.NichtFaellig);

            cancellationToken.ThrowIfCancellationRequested();
            var creator = archiv.Ermittle(cancellationToken);
            if (creator is null) return new(BackendStartsicherungsStatus.NichtVerfuegbar);

            var result = await creator.Erstellen(new SicherungsarchivErstellungsanfrage(vorgangsId), cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (result is null || result.BereitsVorhanden || result.Sicherung is not { } backup ||
                backup.Id != vorgangsId || backup.Art != Sicherungsart.Vollarchiv ||
                !WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig(backup))
                return new(BackendStartsicherungsStatus.Fehlgeschlagen);

            cancellationToken.ThrowIfCancellationRequested();
            if (!await zaehler.Zuruecksetzen(stand.StartsSeitSicherung, cancellationToken).ConfigureAwait(false))
                return new(BackendStartsicherungsStatus.Fehlgeschlagen);
            return new(BackendStartsicherungsStatus.Gesichert);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            return new(counterAdvanced
                ? BackendStartsicherungsStatus.Fehlgeschlagen
                : BackendStartsicherungsStatus.NichtVerfuegbar);
        }
    }
}
