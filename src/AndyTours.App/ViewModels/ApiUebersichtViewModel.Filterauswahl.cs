using AndyTours.Application.Touren;
using AndyTours.Client;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public partial class ApiUebersichtViewModel
{
    private TourenFilterauswahlAnfrage? _angewandterFilterauswahlSnapshot;
    public bool KannAlleFiltertrefferAuswaehlen =>
        !Laedt && !InlineGesperrt && !EditorOffen && _angewandterFilterauswahlSnapshot is not null &&
        _touren is ITourenFilterauswahlClient;

    /// <summary>Ersetzt die Auswahl atomar durch alle Treffer des zuletzt erfolgreich angewandten Filter-Snapshots.</summary>
    public async Task<bool> WaehleAlleFiltertrefferAsync(CancellationToken ct = default)
    {
        if (_inlineBeendet) return false;
        if (Laedt || InlineGesperrt || EditorOffen)
        {
            StatusText = "Die Filtertreffer-Auswahl ist derzeit gesperrt.";
            return false;
        }
        if (_touren is not ITourenFilterauswahlClient client)
        {
            StatusText = "Die Filtertreffer-Auswahl wird vom verbundenen Backend nicht unterstützt.";
            return false;
        }

        var filter = _angewandterFilterauswahlSnapshot;
        if (filter is null)
        {
            StatusText = "Bitte zuerst Filter anwenden und Touren laden.";
            return false;
        }

        var generation = Interlocked.Read(ref _generation);
        var filterIdentitaet = _angewandteAuswahlQuery;
        Laedt = true;
        try
        {
            ct.ThrowIfCancellationRequested();
            var response = await client.Auswaehlen(filter, ct).ConfigureAwait(true);
            ct.ThrowIfCancellationRequested();
            if (!ApiTourenFilterauswahlCommit.TryCommit(
                    _manuelleAuswahl, response, filter, filterIdentitaet, _angewandteAuswahlQuery,
                    generation, Interlocked.Read(ref _generation), _inlineBeendet))
            {
                if (!_inlineBeendet && generation == Interlocked.Read(ref _generation) &&
                    filterIdentitaet == _angewandteAuswahlQuery)
                    StatusText = "Filtertreffer-Antwort passte nicht zum angewandten Filter; die bisherige Auswahl bleibt unverändert.";
                return false;
            }

            SetzeTourenauswahl(_manuelleAuswahl.Count);
            StatusText = $"Alle {_manuelleAuswahl.Count} Treffer der angewandten Filter wurden ausgewählt.";
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        catch (BackendApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.RequestEntityTooLarge)
        {
            if (!_inlineBeendet && generation == Interlocked.Read(ref _generation) &&
                filterIdentitaet == _angewandteAuswahlQuery)
                StatusText = "Die Filtertreffer-Auswahl überschreitet die konfigurierte Servergrenze; die bisherige Auswahl bleibt unverändert.";
            return false;
        }
        catch (BackendApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            if (!_inlineBeendet && generation == Interlocked.Read(ref _generation) &&
                filterIdentitaet == _angewandteAuswahlQuery)
                StatusText = "Die Filtertreffer-Auswahl ist serverseitig nicht konfiguriert; die bisherige Auswahl bleibt unverändert.";
            return false;
        }
        catch (Exception)
        {
            if (!_inlineBeendet && generation == Interlocked.Read(ref _generation))
                StatusText = "Filtertreffer konnten nicht vollständig ausgewählt werden; die bisherige Auswahl bleibt unverändert.";
            return false;
        }
        finally
        {
            if (!_inlineBeendet) Laedt = false;
        }
    }

    private static TourenFilterauswahlAnfrage Auswahlfilter(TourenListenfilter filter) =>
        new(filter.Von, filter.Bis, filter.AktivitaetId, filter.KategorieId, filter.Tabellenfilter);
}
