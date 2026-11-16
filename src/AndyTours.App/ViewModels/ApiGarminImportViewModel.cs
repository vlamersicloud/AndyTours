using System.Collections.ObjectModel;
using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

public partial class ApiGarminImportViewModel : ObservableObject, IDisposable
{
    private const int PageSize = 50;
    private readonly IGarminImportJobs _jobs;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private Guid? _pendingStartId;
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
    private bool CanPrevious() => Idle() && Start > 0;
    private bool CanNext() => Idle() && Auftrag is not null && Start + Kandidaten.Count < Gesamt;

    partial void OnAuftragChanged(GarminImportJobAntwort? value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(WarnungenText));
        NotifyCommands();
    }

    partial void OnStartChanged(int value) => NotifyCommands();
    partial void OnGesamtChanged(int value) => NotifyCommands();
    partial void OnLaedtChanged(bool value) => NotifyCommands();
    partial void OnAendertChanged(bool value) => NotifyCommands();

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
    public Task LadenAktuellenAsync(CancellationToken ct = default) => RefreshAsync(manual: true, ct);

    public Task PollAsync(CancellationToken ct = default) => RefreshAsync(manual: false, ct);

    private async Task RefreshAsync(bool manual, CancellationToken external)
    {
        if (!Idle()) return;
        var generation = Interlocked.Increment(ref _generation);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(external, _lifetime.Token);
        await _refreshLock.WaitAsync(linked.Token);
        try
        {
            if (!Idle() || !IsCurrent(generation, linked.Token)) return;
            Laedt = true;
            var prior = Auftrag;
            var loadedJob = prior is null
                ? await _jobs.AktuellenLaden(linked.Token)
                : await _jobs.Laden(prior.JobId, linked.Token);
            if (!IsCurrent(generation, linked.Token)) return;
            if (loadedJob is null)
            {
                Apply(null, 0, [], 0);
                if (manual) Status = string.Empty;
                return;
            }

            var pageStart = prior?.JobId == loadedJob.JobId ? Start : 0;
            var firstPage = await _jobs.Kandidaten(loadedJob.JobId, pageStart, PageSize, linked.Token);
            if (!IsCurrent(generation, linked.Token)) return;
            pageStart = firstPage.Start;
            if (pageStart > 0 && pageStart >= firstPage.Gesamt)
            {
                pageStart = Math.Max(0, ((firstPage.Gesamt - 1) / PageSize) * PageSize);
                firstPage = await _jobs.Kandidaten(loadedJob.JobId, pageStart, PageSize, linked.Token);
                if (!IsCurrent(generation, linked.Token)) return;
            }
            Apply(loadedJob, firstPage.Gesamt, firstPage.Kandidaten, pageStart);
            if (manual) Status = string.Empty;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (StammdatenException ex)
        {
            if (manual && IsCurrent(generation, linked.Token)) Status = SafeDomainMessage(ex.Art);
        }
        catch
        {
            if (manual && IsCurrent(generation, linked.Token)) Status = "Importstatus oder Kandidaten konnten nicht geladen werden.";
        }
        finally
        {
            if (!_closing && !_disposed) Laedt = false;
            _refreshLock.Release();
        }
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
            var result = await _jobs.Starten(new GarminImportStartAnfrage(requestId), _lifetime.Token);
            if (!IsAlive()) return;
            _pendingStartId = null;
            HatAenderungen = true;
            Apply(result, 0, [], 0);
            Status = "Downloadauftrag gestartet.";
        }
        catch (StammdatenException ex)
        {
            if (IsAlive()) Status = SafeDomainMessage(ex.Art);
        }
        catch
        {
            if (IsAlive())
            {
                HatAenderungen = true;
                Status = "Startstatus unklar. Bitte aktualisieren; derselbe Auftrag wird nicht automatisch erneut gestartet.";
            }
        }
        finally { if (IsAlive()) Aendert = false; }
    }

    [RelayCommand(CanExecute = nameof(CanFortsetzen))]
    private Task FortsetzenAsync() => MutateControl(abbrechen: false);

    [RelayCommand(CanExecute = nameof(CanAbbrechen))]
    private Task AbbrechenAsync() => MutateControl(abbrechen: true);

    private async Task MutateControl(bool abbrechen)
    {
        if (!Idle() || Auftrag is null) return;
        var current = Auftrag;
        var request = new GarminImportSteuerAnfrage(current.Version, Guid.NewGuid());
        Aendert = true;
        try
        {
            var result = abbrechen
                ? await _jobs.Abbrechen(current.JobId, request, _lifetime.Token)
                : await _jobs.Fortsetzen(current.JobId, request, _lifetime.Token);
            if (!IsAlive()) return;
            HatAenderungen = true;
            Apply(result, Gesamt, Kandidaten.ToArray(), Start);
            Status = abbrechen ? "Importauftrag abgebrochen." : "Importauftrag wird fortgesetzt.";
        }
        catch (StammdatenException ex)
        {
            if (IsAlive())
                Status = ex.Art == StammdatenFehlerart.Konflikt
                    ? "Versionskonflikt. Ansicht bitte aktualisieren; die Eingaben bleiben erhalten."
                    : SafeDomainMessage(ex.Art);
        }
        catch
        {
            if (IsAlive())
            {
                HatAenderungen = true;
                Status = "Schreibstatus unklar. Bitte aktualisieren; nicht blind wiederholen.";
            }
        }
        finally { if (IsAlive()) Aendert = false; }
    }

    [RelayCommand(CanExecute = nameof(CanPrevious))]
    private Task VorigeSeiteAsync() => LoadPage(Math.Max(0, Start - PageSize));

    [RelayCommand(CanExecute = nameof(CanNext))]
    private Task NaechsteSeiteAsync() => LoadPage(checked(Start + PageSize));

    private async Task LoadPage(int requestedStart)
    {
        if (!Idle() || Auftrag is null) return;
        var generation = Interlocked.Increment(ref _generation);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        await _refreshLock.WaitAsync(linked.Token);
        try
        {
            if (!Idle() || !IsCurrent(generation, linked.Token)) return;
            Laedt = true;
            var page = await _jobs.Kandidaten(Auftrag.JobId, requestedStart, PageSize, linked.Token);
            if (!IsCurrent(generation, linked.Token)) return;
            Start = page.Start;
            Gesamt = page.Gesamt;
            ReplaceCandidates(page.Kandidaten);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch { if (IsAlive()) Status = "Kandidatenseite konnte nicht geladen werden."; }
        finally
        {
            if (IsAlive()) Laedt = false;
            _refreshLock.Release();
        }
    }

    private void Apply(GarminImportJobAntwort? job, int total, IEnumerable<GarminImportKandidatAntwort> items, int start)
    {
        Auftrag = job;
        Gesamt = total;
        Start = start;
        ReplaceCandidates(items);
    }

    private void ReplaceCandidates(IEnumerable<GarminImportKandidatAntwort> items)
    {
        Kandidaten.Clear();
        foreach (var item in items) Kandidaten.Add(item);
        NotifyCommands();
    }

    private bool IsAlive() => !_closing && !_disposed;
    private bool IsCurrent(long generation, CancellationToken ct) =>
        IsAlive() && !ct.IsCancellationRequested && generation == Volatile.Read(ref _generation);

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
        _lifetime.Cancel();
        NotifyCommands();
    }

    public void Dispose()
    {
        if (_disposed) return;
        BeginClose();
        _disposed = true;
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
