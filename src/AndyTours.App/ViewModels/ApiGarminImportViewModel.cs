using System.Collections.ObjectModel;
using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

/// <summary>Job control and safe status only; FIT review/import is not yet connected.</summary>
public partial class ApiGarminImportViewModel : ObservableObject, IDisposable
{
    private readonly IGarminImportJobs _jobs;
    private readonly CancellationTokenSource _readLifetime = new();
    private Guid? _pendingStartId;
    private string? _pendingStartVersion;
    private bool _closing;
    private bool _disposed;
    private long _generation;

    [ObservableProperty] private GarminImportJobAntwort? _auftrag;
    [ObservableProperty] private int _start;
    [ObservableProperty] private int _gesamt;
    [ObservableProperty] private bool _laedt;
    [ObservableProperty] private bool _aendert;
    [ObservableProperty] private bool _hatAenderungen;
    [ObservableProperty] private string _status = string.Empty;

    public ObservableCollection<GarminImportKandidatAntwort> Kandidaten { get; } = [];

    public ApiGarminImportViewModel(IGarminImportJobs jobs) =>
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));

    public string StatusText => Auftrag is null ? "Kein Importauftrag." : Auftrag.Status switch
    {
        GarminImportJobStatus.Wartend => "Wartet auf Verarbeitung.",
        GarminImportJobStatus.Laeuft => "Wird verarbeitet.",
        GarminImportJobStatus.PaketBereit => "Paket bereit.",
        GarminImportJobStatus.Unterbrochen => "Unterbrochen; kann fortgesetzt werden.",
        GarminImportJobStatus.Abgebrochen => "Abgebrochen; kann fortgesetzt werden.",
        GarminImportJobStatus.Fertig => "Download abgeschlossen.",
        _ => "Unbekannter Status."
    };

    public string WarnungenText => Auftrag is null || Auftrag.Warncodes.Count == 0
        ? string.Empty
        : string.Join(" ", Auftrag.Warncodes.Select(TranslateWarning));

    public bool KannFortsetzen => Idle() && Auftrag is not null &&
        (Auftrag.Status is GarminImportJobStatus.Unterbrochen or GarminImportJobStatus.Abgebrochen ||
         Auftrag.Status == GarminImportJobStatus.PaketBereit && Auftrag.WeitereMoeglich);

    public bool KannAbbrechen => Idle() && Auftrag is not null &&
        Auftrag.Status is not (GarminImportJobStatus.Fertig or GarminImportJobStatus.Abgebrochen);

    private bool Idle() => !_closing && !_disposed && !Laedt && !Aendert;
    private bool CanFortsetzen() => KannFortsetzen;
    private bool CanAbbrechen() => KannAbbrechen;

    partial void OnAuftragChanged(GarminImportJobAntwort? value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(WarnungenText));
        FortsetzenCommand.NotifyCanExecuteChanged();
        AbbrechenCommand.NotifyCanExecuteChanged();
        VorigeSeiteCommand.NotifyCanExecuteChanged();
        NaechsteSeiteCommand.NotifyCanExecuteChanged();
    }

    partial void OnLaedtChanged(bool value) => NotifyCommands();
    partial void OnAendertChanged(bool value) => NotifyCommands();
    partial void OnStartChanged(int value) => NaechsteSeiteCommand.NotifyCanExecuteChanged();

    private void NotifyCommands()
    {
        LadenAktuellenCommand.NotifyCanExecuteChanged();
        StartenCommand.NotifyCanExecuteChanged();
        FortsetzenCommand.NotifyCanExecuteChanged();
        AbbrechenCommand.NotifyCanExecuteChanged();
        VorigeSeiteCommand.NotifyCanExecuteChanged();
        NaechsteSeiteCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(Idle))]
    public async Task LadenAktuellenAsync(CancellationToken ct = default)
    {
        if (!Idle()) return;
        var generation = Interlocked.Increment(ref _generation);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _readLifetime.Token);
        Laedt = true;
        try
        {
            var result = await _jobs.AktuellenLaden(linked.Token).ConfigureAwait(false);
            if (!StillCurrent(generation, linked.Token)) return;
            Apply(result, []);
            Status = string.Empty;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (StammdatenException ex) { Status = SafeDomainMessage(ex.Art); }
        catch { Status = "Importstatus konnte nicht geladen werden."; }
        finally { Laedt = false; }
    }

    [RelayCommand(CanExecute = nameof(Idle))]
    private async Task StartenAsync()
    {
        if (!Idle()) return;
        _pendingStartId ??= Guid.NewGuid();
        var requestId = _pendingStartId.Value;
        Aendert = true;
        try
        {
            var result = await _jobs.Starten(new GarminImportStartAnfrage(requestId), _readLifetime.Token).ConfigureAwait(false);
            if (_closing) return;
            _pendingStartId = null;
            HatAenderungen = true;
            Apply(result, []);
            Status = "Downloadauftrag gestartet.";
        }
        catch (StammdatenException ex) { Status = SafeDomainMessage(ex.Art); }
        catch { Status = "Startstatus unklar. Bitte zuerst aktualisieren; nicht blind erneut starten."; }
        finally { Aendert = false; }
    }

    [RelayCommand(CanExecute = nameof(CanFortsetzen))]
    private async Task FortsetzenAsync()
    {
        if (!KannFortsetzen || Auftrag is null) return;
        await MutateControl(abbrechen: false);
    }

    [RelayCommand(CanExecute = nameof(CanAbbrechen))]
    private async Task AbbrechenAsync()
    {
        if (!KannAbbrechen || Auftrag is null) return;
        await MutateControl(abbrechen: true);
    }

    private async Task MutateControl(bool abbrechen)
    {
        var current = Auftrag!;
        var request = new GarminImportSteuerAnfrage(current.Version, Guid.NewGuid());
        Aendert = true;
        try
        {
            var result = abbrechen
                ? await _jobs.Abbrechen(current.JobId, request, _readLifetime.Token).ConfigureAwait(false)
                : await _jobs.Fortsetzen(current.JobId, request, _readLifetime.Token).ConfigureAwait(false);
            if (_closing) return;
            HatAenderungen = true;
            Apply(result, Kandidaten.ToArray());
            Status = abbrechen ? "Importauftrag abgebrochen." : "Importauftrag wird fortgesetzt.";
        }
        catch (StammdatenException ex)
        {
            Status = ex.Art == StammdatenFehlerart.Konflikt
                ? "Versionskonflikt. Ansicht bitte aktualisieren; der Auftrag wurde nicht automatisch erneut geändert."
                : SafeDomainMessage(ex.Art);
        }
        catch { Status = "Schreibstatus unklar. Bitte aktualisieren; nicht blind erneut wiederholen."; }
        finally { Aendert = false; }
    }

    [RelayCommand(CanExecute = nameof(Idle))]
    private async Task VorigeSeiteAsync()
    {
        if (!Idle() || Start == 0) return;
        Start = Math.Max(0, Start - PageSize);
        await LoadPage();
    }

    [RelayCommand(CanExecute = nameof(CanNextPage))]
    private async Task NaechsteSeiteAsync()
    {
        if (!CanNextPage()) return;
        Start = checked(Start + PageSize);
        await LoadPage();
    }

    private const int PageSize = 50;
    private bool CanNextPage() => Idle() && Auftrag is not null && Start + Kandidaten.Count < Gesamt;

    private async Task LoadPage()
    {
        if (Auftrag is null || !Idle()) return;
        var jobId = Auftrag.JobId;
        var generation = Interlocked.Increment(ref _generation);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_readLifetime.Token);
        Laedt = true;
        try
        {
            var page = await _jobs.Kandidaten(jobId, Start, PageSize, linked.Token).ConfigureAwait(false);
            if (!StillCurrent(generation, linked.Token) || Auftrag?.JobId != jobId) return;
            Gesamt = page.Gesamt;
            Kandidaten.Clear();
            foreach (var candidate in page.Kandidaten) Kandidaten.Add(candidate);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch { Status = "Kandidaten konnten nicht geladen werden."; }
        finally { Laedt = false; }
    }

    public async Task PollAsync(CancellationToken ct = default)
    {
        if (!Idle()) return;
        var generation = Interlocked.Increment(ref _generation);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _readLifetime.Token);
        Laedt = true;
        try
        {
            var updated = await _jobs.AktuellenLaden(linked.Token).ConfigureAwait(false);
            if (!StillCurrent(generation, linked.Token)) return;
            if (Auftrag is not null && updated?.JobId != Auftrag.JobId) return;
            if (updated is not null && Auftrag?.Version != updated.Version)
            {
                Apply(updated, Kandidaten.ToArray());
                await LoadPage();
            }
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch { /* Background refresh is intentionally quiet. */ }
        finally { Laedt = false; }
    }

    private bool StillCurrent(long generation, CancellationToken ct) =>
        !_closing && !_disposed && !ct.IsCancellationRequested && generation == Volatile.Read(ref _generation);

    private void Apply(GarminImportJobAntwort? job, IEnumerable<GarminImportKandidatAntwort> candidates)
    {
        Auftrag = job;
        Kandidaten.Clear();
        foreach (var candidate in candidates) Kandidaten.Add(candidate);
        Gesamt = job?.Bearbeitet ?? 0;
        Start = 0;
    }

    private static string SafeDomainMessage(StammdatenFehlerart art) => art switch
    {
        StammdatenFehlerart.Konflikt => "Der Importauftrag wurde zwischenzeitlich geändert.",
        StammdatenFehlerart.NichtGefunden => "Der Importauftrag wurde nicht gefunden.",
        _ => "Die Importanfrage ist ungültig."
    };

    private static string TranslateWarning(string code) => code switch
    {
        GarminImportWarncodes.Netzwerk => "Netzwerkverbindung fehlgeschlagen.",
        GarminImportWarncodes.Zeitlimit => "Zeitlimit erreicht.",
        GarminImportWarncodes.Abgebrochen => "Vorgang abgebrochen.",
        GarminImportWarncodes.FitUngueltig => "FIT-Datei ungültig.",
        GarminImportWarncodes.ArchivUngueltig => "Archiv ungültig.",
        GarminImportWarncodes.DateiFehler => "Dateizugriff fehlgeschlagen.",
        GarminImportWarncodes.SchluesselFehlt => "Garmin-Schlüssel fehlt.",
        GarminImportWarncodes.RateLimit => "Garmin begrenzt derzeit Anfragen.",
        _ => "Ein unbekannter Hinweis ist aufgetreten."
    };

    public void BeginClose()
    {
        if (_closing) return;
        _closing = true;
        _generation++;
        _readLifetime.Cancel();
        NotifyCommands();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        BeginClose();
        _readLifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
