namespace AndyTours.App.ViewModels;

using AndyTours.Contracts.V1;
using AndyTours.Application.Touren;

public partial class ApiUebersichtViewModel
{
    private long _inlineGeneration;
    private bool _inlineBusy;
    private ApiTourInlineSpeicher? _inlineSchreiber;
    private bool _inlineEditAktiv;
    private bool _inlineBeendet;
    private int _inlineFilterInvalidierung;
    public bool InlineFilterAenderungAusstehend => _inlineFilterInvalidierung != 0;

    private bool MerkeInlineFilterAenderung(bool nurTabelle)
    {
        if (!InlineGesperrt) return false;
        _inlineFilterInvalidierung = Math.Max(_inlineFilterInvalidierung, nurTabelle ? 1 : 2);
        _inlineGeneration++;
        Interlocked.Increment(ref _generation);
        InlineNeuLadenNoetig = true;
        return true;
    }

    private void WendeInlineFilterInvalidierungAn()
    {
        if (_inlineBeendet || InlineGesperrt || _inlineFilterInvalidierung == 0) return;
        var mode = _inlineFilterInvalidierung;
        _inlineFilterInvalidierung = 0;
        if (mode == 2) FilterGeaendert(); else TabellenfilterGeaendert();
        StatusText = "Filter während Inline-Bearbeitung geändert. Änderung nicht erneut senden; bitte Liste aktualisieren.";
    }

    private void BenachrichtigeInlineZustand()
    {
        OnPropertyChanged(nameof(InlineGesperrt));
        OnPropertyChanged(nameof(HatTourenauswahl));
        OnPropertyChanged(nameof(FilterControlsEnabled));
        AktualisierenCommand.NotifyCanExecuteChanged();
        NaechsteSeiteCommand.NotifyCanExecuteChanged();
        VorherigeSeiteCommand.NotifyCanExecuteChanged();
        BenachrichtigeTourCommands();
    }
    public bool InlineGesperrt => _inlineEditAktiv || _inlineBusy;
    public bool InlineNeuLadenNoetig { get; private set; }
    public void SetzeInlineBearbeitung(bool aktiv)
    {
        if (_inlineEditAktiv == aktiv) return;
        _inlineEditAktiv = aktiv;
        BenachrichtigeInlineZustand();
        if (!aktiv) WendeInlineFilterInvalidierungAn();
    }
    private (IReadOnlyList<(long Id,string Name)> Activities, IReadOnlyList<(long Id,string Name)> Equipment, IReadOnlyList<string> Areas)? _inlineLookups;
    public ApiTourInlineEntwurf InlineEntwurfFuer(TourAntwort tour) => ApiTourInlineEntwurf.Beginnen(tour);
    public bool InlineLookupsBereit => _inlineLookups is not null;
    public IReadOnlyList<(long Id,string Name)> InlineAktivitaeten => _inlineLookups?.Activities ?? Array.Empty<(long,string)>();
    public IReadOnlyList<(long Id,string Name)> InlineEquipments => _inlineLookups?.Equipment ?? Array.Empty<(long,string)>();
    public IReadOnlyList<string> InlineTrainingsbereiche => _inlineLookups?.Areas ?? Array.Empty<string>();

    public async Task<bool> LadeInlineLookupsAsync(CancellationToken cancellationToken = default)
    {
        var generation = ++_inlineGeneration;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await _stammdatenLeser.Laden(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (_inlineBeendet || generation != _inlineGeneration) return false;
            if (snapshot is null || snapshot.Aktivitaeten is null || snapshot.Equipments is null ||
                snapshot.Trainingsbereiche is null || snapshot.Aktivitaeten.Count == 0 ||
                snapshot.Aktivitaeten.Any(x => x is null || x.Id <= 0 || string.IsNullOrWhiteSpace(x.Name)) ||
                snapshot.Equipments.Any(x => x is null || x.Id <= 0 || string.IsNullOrWhiteSpace(x.Name)) ||
                snapshot.Trainingsbereiche.Any(x => x is null || x.Length > 200 || x.Length > 0 && string.IsNullOrWhiteSpace(x)) ||
                snapshot.Aktivitaeten.Select(x => x.Id).Distinct().Count() != snapshot.Aktivitaeten.Count ||
                snapshot.Equipments.Select(x => x.Id).Distinct().Count() != snapshot.Equipments.Count) { _inlineLookups = null; return false; }
            _inlineLookups = (Array.AsReadOnly(snapshot.Aktivitaeten.Select(x => (x.Id, x.Name)).ToArray()),
                Array.AsReadOnly(snapshot.Equipments.Select(x => (x.Id, Name: x.Ausgemustert ? x.Name + " (ausgemustert)" : x.Name)).ToArray()),
                Array.AsReadOnly(snapshot.Trainingsbereiche.ToArray()));
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch { if (generation == _inlineGeneration) _inlineLookups = null; return false; }
    }

    public async Task<bool> SpeichereInlineAsync(ApiTourInlineEntwurf draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (_inlineBeendet || InlineFilterAenderungAusstehend || InlineNeuLadenNoetig || _inlineBusy || Laedt || EditorOffen || AuswahlAnzahl != 1 || AusgewaehlteTour?.Id != draft.TourId ||
            Touren.Count(t => t.Id == draft.TourId) != 1 || draft.Version.Length == 0 || !draft.HatAenderungen) return false;
        var generation = _inlineGeneration; var queryGeneration = _generation; _inlineBusy = true;
        BenachrichtigeInlineZustand();
        try
        {
            _inlineSchreiber ??= new ApiTourInlineSpeicher(_touren);
            var result = await _inlineSchreiber.SpeichernAsync(draft, cancellationToken);
            if (_inlineBeendet || generation != _inlineGeneration || queryGeneration != _generation ||
                AusgewaehlteTour?.Id != draft.TourId) return false;
            InlineNeuLadenNoetig = result is ApiInlineSpeicherErgebnis.Gespeichert or ApiInlineSpeicherErgebnis.Konflikt or ApiInlineSpeicherErgebnis.NichtGefunden or ApiInlineSpeicherErgebnis.Unklar;
            if (result == ApiInlineSpeicherErgebnis.Gespeichert) return true;
            StatusText = result switch
            {
                ApiInlineSpeicherErgebnis.Ungueltig => "Das Backend hat die Eingabe abgelehnt. Nicht gespeichert; Eingaben prüfen.",
                ApiInlineSpeicherErgebnis.Konflikt => "Versionskonflikt. Liste aktualisieren; Änderungen nicht automatisch erneut speichern.",
                ApiInlineSpeicherErgebnis.NichtGefunden => "Tour nicht mehr vorhanden. Liste aktualisieren.",
                ApiInlineSpeicherErgebnis.Abgebrochen => "Speichern vor dem Senden abgebrochen.",
                ApiInlineSpeicherErgebnis.Unveraendert => "Keine Änderung.",
                ApiInlineSpeicherErgebnis.Beschaeftigt => "Ein Speichervorgang läuft bereits.",
                _ => "Speicherstatus unklar. Bitte Liste aktualisieren; nicht automatisch erneut speichern."
            };
            return false;
        }
        finally { _inlineBusy = false; if (!_inlineBeendet) { BenachrichtigeInlineZustand(); WendeInlineFilterInvalidierungAn(); } }
    }
    public void VerwirfInlineEntwurf() => _inlineGeneration++;
    public void BeendeInline()
    {
        if (_inlineBeendet) return;
        _inlineBeendet = true;
        _inlineGeneration++;
        Interlocked.Increment(ref _generation);
        SetzeInlineBearbeitung(false);
    }
}
