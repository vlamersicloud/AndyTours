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
    private string? _version;
    private bool _disposed;

    public ApiPlanungsProfilSitzung(IPlanungsProfil port, PlanungsProfilAntwort initial)
    {
        _port = port ?? throw new ArgumentNullException(nameof(port));
        if (!Valid(initial)) throw new ArgumentException("Initial planning profile is invalid.", nameof(initial));
        Profil = initial.Profil;
        _version = initial.Version;
    }

    public CourseFahrprofil Profil { get; private set; }
    public string Hinweis { get; private set; } = string.Empty;
    public event EventHandler? Geaendert;

    public async Task Setzen(CourseFahrprofil profil, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(profil)) { SetHint("Das Planungsprofil ist ungültig."); return; }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        try { await _serial.WaitAsync(linked.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }
        try
        {
            if (_disposed || linked.IsCancellationRequested) return;
            string? version = _version;
            if (version is null)
            {
                PlanungsProfilAntwort current;
                try { current = await _port.Laden(linked.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (linked.IsCancellationRequested) { return; }
                catch { SetHint("Planungsprofil konnte nicht synchronisiert werden."); return; }
                if (!Valid(current)) { _version = null; SetHint("Serverantwort zum Planungsprofil ungültig."); return; }
                Profil = current.Profil; version = current.Version; _version = version; Notify();
                if (Profil == profil) { SetHint(string.Empty); return; }
            }
            if (Profil == profil) { SetHint(string.Empty); return; }
            try
            {
                var saved = await _port.Speichern(new(version, profil), linked.Token).ConfigureAwait(false);
                if (!Valid(saved) || saved.Profil != profil) { _version = null; SetHint("Serverantwort zum Planungsprofil ungültig."); return; }
                _version = saved.Version; Profil = saved.Profil; SetHint(string.Empty);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested) { _version = null; return; }
            catch (StammdatenException ex) when (ex.Art == StammdatenFehlerart.Konflikt) { _version = null; SetHint("Planungsprofil wurde geändert; bitte erneut wählen."); }
            catch { _version = null; SetHint("Planungsprofil konnte nicht gespeichert werden; bitte erneut wählen."); }
        }
        finally { _serial.Release(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _lifetime.Cancel(); Geaendert = null;
    }

    private static bool Valid(PlanungsProfilAntwort? value) => value is not null && value.Version is { Length: 64 } version && version.All(Uri.IsHexDigit) && Enum.IsDefined(value.Profil);
    private void SetHint(string hint) { if (_disposed) return; Hinweis = hint; Notify(); }
    private void Notify() { if (!_disposed) Geaendert?.Invoke(this, EventArgs.Empty); }
}
