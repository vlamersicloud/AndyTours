using AndyTours.Application.Touren;
namespace AndyTours.App.ViewModels;

/// <summary>One in-flight CAS request; no reads, refreshes or automatic retries.</summary>
public sealed class ApiTourInlineSpeicher(ITourenSpeicher touren)
{
    private int _sendet;
    public async Task<ApiInlineSpeicherErgebnis> SpeichernAsync(ApiTourInlineEntwurf draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.HatAenderungen) return ApiInlineSpeicherErgebnis.Unveraendert;
        if (ct.IsCancellationRequested) return ApiInlineSpeicherErgebnis.Abgebrochen;
        if (Interlocked.CompareExchange(ref _sendet, 1, 0) != 0) return ApiInlineSpeicherErgebnis.Beschaeftigt;
        try
        {
            if (ct.IsCancellationRequested) return ApiInlineSpeicherErgebnis.Abgebrochen;
            var antwort = await touren.Aendern(draft.TourId, new(draft.Version, draft.Werte), ct);
            if (antwort is null || antwort.Id != draft.TourId || string.IsNullOrWhiteSpace(antwort.Version) ||
                antwort.Werte is null || antwort.Werte.KategorieIds is null || antwort.Werte.Name is null ||
                antwort.Werte.Kommentar is null || antwort.Werte.Trainingsbereich is null)
                return ApiInlineSpeicherErgebnis.Unklar;
            return ApiInlineSpeicherErgebnis.Gespeichert;
        }
        catch (UngueltigeTourException) { return ApiInlineSpeicherErgebnis.Ungueltig; }
        catch (TourVersionskonfliktException) { return ApiInlineSpeicherErgebnis.Konflikt; }
        catch (TourNichtGefundenException) { return ApiInlineSpeicherErgebnis.NichtGefunden; }
        catch (Exception) { return ApiInlineSpeicherErgebnis.Unklar; }
        finally { Interlocked.Exchange(ref _sendet, 0); }
    }
}
public enum ApiInlineSpeicherErgebnis
{
    Unveraendert, Abgebrochen, Beschaeftigt, Gespeichert, Konflikt, NichtGefunden, Ungueltig, Unklar
}
