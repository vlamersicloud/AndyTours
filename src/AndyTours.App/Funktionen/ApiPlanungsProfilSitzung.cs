using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.App.Funktionen;

/// <summary>Serializes profile writes for one API-root planning session using server CAS versions.</summary>
public sealed class ApiPlanungsProfilSitzung : IDisposable
{
    private readonly IPlanungsProfil _port;
    private readonly SemaphoreSlim _serial = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _lifetimeToken;
    private readonly SynchronizationContext? _uiContext;
    private string? _version;
    private long _generation;
    private bool _disposed;

    public ApiPlanungsProfilSitzung(IPlanungsProfil port, PlanungsProfilAntwort initial, SynchronizationContext? uiContext = null)
    {
        _port = port ?? throw new ArgumentNullException(nameof(port));
        if (!Valid(initial)) throw new ArgumentException("Initial planning profile is invalid.", nameof(initial));
        Profil = initial.Profil;
        _version = initial.Version;
        _uiContext = uiContext ?? SynchronizationContext.Current;
        _lifetimeToken = _lifetime.Token;
    }

    public CourseFahrprofil Profil { get; private set; }
    public string Hinweis { get; private set; } = string.Empty;
    public event EventHandler? Geaendert;

    public async Task Setzen(CourseFahrprofil profil, CancellationToken cancellationToken)
    {
        if (_disposed) return;
        long generation = Interlocked.Increment(ref _generation);
        if (!Enum.IsDefined(profil)) { await SetHintAsync("Das Planungsprofil ist ungültig.", generation); return; }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        try { await _serial.WaitAsync(linked.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }
        try
        {
            if (_disposed || linked.IsCancellationRequested || generation != Volatile.Read(ref _generation)) return;
            string? version = _version;
            if (version is null)
            {
                PlanungsProfilAntwort current;
                try { current = await _port.Laden(linked.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (linked.IsCancellationRequested) { return; }
                catch { if (!linked.IsCancellationRequested) await SetHintAsync("Planungsprofil konnte nicht synchronisiert werden.", generation); return; }
                if (linked.IsCancellationRequested || _disposed) return;
                if (!Valid(current)) { _version = null; await SetHintAsync("Serverantwort zum Planungsprofil ungültig.", generation); return; }
                Profil = current.Profil; version = current.Version; _version = version;
                if (generation != Volatile.Read(ref _generation)) return;
                if (Profil == profil) { await SetHintAsync(string.Empty, generation); return; }
            }
            if (Profil == profil) { await SetHintAsync(string.Empty, generation); return; }
            try
            {
                var saved = await _port.Speichern(new(version, profil), linked.Token).ConfigureAwait(false);
                if (linked.IsCancellationRequested || _disposed) { _version = null; return; }
                if (!Valid(saved) || saved.Profil != profil) { _version = null; await SetHintAsync("Serverantwort zum Planungsprofil ungültig.", generation); return; }
                // Even a superseded confirmed write advances CAS state for the next queued intent.
                _version = saved.Version; Profil = saved.Profil;
                await SetHintAsync(string.Empty, generation);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested) { _version = null; return; }
            catch (StammdatenException ex) when (ex.Art == StammdatenFehlerart.Konflikt) { _version = null; await SetHintAsync("Planungsprofil wurde geändert; bitte erneut wählen.", generation); }
            catch { _version = null; if (!linked.IsCancellationRequested) await SetHintAsync("Planungsprofil konnte nicht gespeichert werden; bitte erneut wählen.", generation); }
        }
        finally { _serial.Release(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Interlocked.Increment(ref _generation);
        _lifetime.Cancel();
        Geaendert = null;
        _lifetime.Dispose();
    }

    private static bool Valid(PlanungsProfilAntwort? value) => value is not null && value.Version is { Length: 64 } version && version.All(Uri.IsHexDigit) && Enum.IsDefined(value.Profil);

    private Task SetHintAsync(string hint, long generation) => OnUiAsync(() =>
    {
        if (_disposed || generation != Volatile.Read(ref _generation)) return;
        Hinweis = hint;
        Geaendert?.Invoke(this, EventArgs.Empty);
    });

    private Task OnUiAsync(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext) { action(); return Task.CompletedTask; }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiContext.Post(_ => { try { action(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } }, null);
        return completion.Task;
    }
}
