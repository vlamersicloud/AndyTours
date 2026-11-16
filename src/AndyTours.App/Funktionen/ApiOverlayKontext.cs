using System.Collections.ObjectModel;
using System.ComponentModel;
using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.App.Funktionen;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using CoreFarbe = AndyTours.Core.Modell.Overlayfarbe;

namespace AndyTours.App.Funktionen;

/// <summary>Immutable applied API overlay state, scoped to one API root/session.</summary>
public sealed record ApiOverlayAnzeige(
    OverlayBestandAntwort Bestand,
    IReadOnlyList<Overlay> Eingeschaltete,
    CoreFarbe Farbe,
    string Hinweis);

/// <summary>Per-root overlay snapshot and enabled geometry cache; never uses the legacy static overlay dataset.</summary>
public sealed class ApiOverlayKontext : IDisposable
{
    private readonly IOverlayLeser _leser;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _lebensdauer = new();
    private readonly CancellationToken _lebensdauerToken;
    private IReadOnlyDictionary<(long Id, string Version), Overlay> _cache =
        new ReadOnlyDictionary<(long, string), Overlay>(new Dictionary<(long, string), Overlay>());
    private long _generation;
    private bool _beendet;

    public ApiOverlayKontext(IOverlayLeser leser, Action? verwaltungOeffnen = null)
    {
        _leser = leser ?? throw new ArgumentNullException(nameof(leser));
        VerwaltungOeffnen = verwaltungOeffnen;
        _uiContext = SynchronizationContext.Current;
        _lebensdauerToken = _lebensdauer.Token;
    }

    public ApiOverlayAnzeige? Anzeige { get; private set; }
    public string LastHinweis { get; private set; } = string.Empty;
    public Action? VerwaltungOeffnen { get; }
    public event EventHandler? Geaendert;
    public event EventHandler<Overlay>? GanzesOverlayAngefordert;

    public async Task LadenAsync(CancellationToken cancellationToken)
    {
        if (_beendet) return;
        long generation = Interlocked.Increment(ref _generation);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lebensdauerToken);
        CancellationToken ct = linked.Token;
        try
        {
            ct.ThrowIfCancellationRequested();
            var bestand = await _leser.Laden(ct);
            ct.ThrowIfCancellationRequested();
            if (bestand is null || bestand.Ordner is null || bestand.Overlays is null)
                throw new InvalidOperationException("Overlay-Antwort ungültig.");

            var priorCache = _cache;
            var nextCache = new Dictionary<(long Id, string Version), Overlay>();
            var enabled = new List<Overlay>();
            var failedNames = new List<string>();
            foreach (var meta in bestand.Overlays)
            {
                ct.ThrowIfCancellationRequested();
                if (!meta.IstEingeschaltet) continue;
                Overlay mapped;
                if (!priorCache.TryGetValue((meta.Id, meta.Version), out mapped!))
                {
                    var content = await _leser.Inhalt(meta.Id, meta.Version, ct);
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        mapped = await Task.Run(() => ApiOverlayMapper.AlsOverlay(meta, content, ct), ct);
                    }
                    catch (InvalidOperationException)
                    {
                        if (failedNames.Count < 20 && !string.IsNullOrWhiteSpace(meta.Name)) failedNames.Add(meta.Name);
                        continue;
                    }
                }
                nextCache[(meta.Id, meta.Version)] = mapped;
                enabled.Add(mapped);
            }

            ct.ThrowIfCancellationRequested();
            var message = failedNames.Count == 0 ? string.Empty :
                "Einige eingeschaltete Overlays konnten nicht geladen werden: " + string.Join(", ", failedNames) +
                (bestand.Overlays.Count(o => o.IstEingeschaltet) > failedNames.Count ? " und weitere." : ".");
            var next = new ApiOverlayAnzeige(bestand,
                new ReadOnlyCollection<Overlay>(enabled), ApiOverlayMapper.Farbe(bestand.GemeinsameFarbe), message);
            await AufUiThreadAsync(() =>
            {
                ct.ThrowIfCancellationRequested();
                if (_beendet || generation != Volatile.Read(ref _generation)) return;
                _cache = new ReadOnlyDictionary<(long, string), Overlay>(nextCache);
                Anzeige = next;
                LastHinweis = message;
                Geaendert?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (StammdatenException ex) when (ex.Art is StammdatenFehlerart.NichtGefunden or StammdatenFehlerart.Konflikt)
        {
            await MeldeLadefehlerAsync(generation, "Der Overlay-Bestand wurde geändert. Bitte neu laden.");
        }
        catch (Exception)
        {
            await MeldeLadefehlerAsync(generation, "Overlays konnten nicht vollständig geladen werden. Bitte Verbindung und Bestand prüfen.");
        }
    }

    public void ZeigeGanz(long overlayId)
    {
        var overlay = Anzeige?.Eingeschaltete.FirstOrDefault(o => o.Id == overlayId);
        if (!_beendet && overlay is not null) GanzesOverlayAngefordert?.Invoke(this, overlay);
    }

    public void Dispose()
    {
        if (_beendet) return;
        _beendet = true;
        Interlocked.Increment(ref _generation);
        _lebensdauer.Cancel();
        _cache = new ReadOnlyDictionary<(long, string), Overlay>(new Dictionary<(long, string), Overlay>());
        _lebensdauer.Dispose();
    }

    private async Task MeldeLadefehlerAsync(long generation, string hinweis)
    {
        await AufUiThreadAsync(() =>
        {
            if (_beendet || generation != Volatile.Read(ref _generation)) return;
            LastHinweis = hinweis;
            Geaendert?.Invoke(this, EventArgs.Empty);
        });
    }

    private Task AufUiThreadAsync(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
        {
            action();
            return Task.CompletedTask;
        }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiContext.Post(_ =>
        {
            try { action(); completion.SetResult(); }
            catch (Exception ex) { completion.SetException(ex); }
        }, null);
        return completion.Task;
    }
}
