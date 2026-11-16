using System.Collections.ObjectModel;
using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using CoreFarbe = AndyTours.Core.Modell.Overlayfarbe;

namespace AndyTours.App.Funktionen;

public sealed record ApiOverlayAnzeige(
    OverlayBestandAntwort Bestand,
    IReadOnlyList<Overlay> Eingeschaltete,
    CoreFarbe Farbe,
    string Hinweis);

/// <summary>Per-root API overlay state; does not share the legacy static dataset.</summary>
public sealed class ApiOverlayKontext : IDisposable
{
    private readonly IOverlayLeser _leser;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _lebensdauer = new();
    private readonly CancellationToken _lebensdauerToken;
    private IReadOnlyDictionary<(long Id, string Version), Overlay> _cache = EmptyCache();
    private long _generation;
    private bool _beendet;

    public ApiOverlayKontext(IOverlayLeser leser, Action? verwaltungOeffnen = null, SynchronizationContext? uiContext = null)
    {
        _leser = leser ?? throw new ArgumentNullException(nameof(leser));
        VerwaltungOeffnen = verwaltungOeffnen;
        _uiContext = uiContext ?? SynchronizationContext.Current;
        _lebensdauerToken = _lebensdauer.Token;
    }

    public ApiOverlayAnzeige? Anzeige { get; private set; }
    public string LastHinweis { get; private set; } = string.Empty;
    public Action? VerwaltungOeffnen { get; private set; }
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
            int failedCount = 0;
            foreach (var meta in bestand.Overlays)
            {
                ct.ThrowIfCancellationRequested();
                if (!meta.IstEingeschaltet) continue;
                Overlay mapped;
                if (!priorCache.TryGetValue((meta.Id, meta.Version), out mapped!))
                {
                    try
                    {
                        var content = await _leser.Inhalt(meta.Id, meta.Version, ct);
                        ct.ThrowIfCancellationRequested();
                        mapped = await Task.Run(() => ApiOverlayMapper.AlsOverlay(meta, content, ct), ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (StammdatenException ex) when (ex.Art is StammdatenFehlerart.NichtGefunden or StammdatenFehlerart.Konflikt) { throw; }
                    catch (StammdatenException ex) when (ex.Art == StammdatenFehlerart.Ungueltig)
                    {
                        RecordGeometryFailure(meta.Name, failedNames, ref failedCount);
                        continue;
                    }
                    catch (InvalidOperationException)
                    {
                        RecordGeometryFailure(meta.Name, failedNames, ref failedCount);
                        continue;
                    }
                }
                nextCache[(meta.Id, meta.Version)] = mapped;
                enabled.Add(mapped);
            }

            ct.ThrowIfCancellationRequested();
            string message = failedCount == 0 ? string.Empty :
                "Einige eingeschaltete Overlays konnten nicht geladen werden" +
                (failedNames.Count == 0 ? "." : ": " + string.Join(", ", failedNames) +
                    (failedCount > failedNames.Count ? " und weitere." : "."));
            var safeFolders = new ReadOnlyCollection<OverlayOrdnerAntwort>(bestand.Ordner.ToList());
            var safeItems = new ReadOnlyCollection<OverlayMetadatenAntwort>(bestand.Overlays.ToList());
            var safeBestand = bestand with { Ordner = safeFolders, Overlays = safeItems };
            var next = new ApiOverlayAnzeige(safeBestand,
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
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (StammdatenException ex) when (ex.Art is StammdatenFehlerart.NichtGefunden or StammdatenFehlerart.Konflikt)
        {
            await MeldeLadefehlerAsync(generation, "Der Overlay-Bestand wurde geändert. Bitte neu laden.", ct);
        }
        catch (Exception)
        {
            await MeldeLadefehlerAsync(generation, "Overlays konnten nicht vollständig geladen werden. Bitte Verbindung und Bestand prüfen.", ct);
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
        Anzeige = null;
        LastHinweis = string.Empty;
        _cache = EmptyCache();
        VerwaltungOeffnen = null;
        Geaendert = null;
        GanzesOverlayAngefordert = null;
        _lebensdauer.Dispose();
    }

    private async Task MeldeLadefehlerAsync(long generation, string hint, CancellationToken ct)
    {
        await AufUiThreadAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            if (_beendet || generation != Volatile.Read(ref _generation)) return;
            LastHinweis = hint;
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

    private static void RecordGeometryFailure(string? name, List<string> names, ref int count)
    {
        count++;
        if (names.Count < 20 && !string.IsNullOrWhiteSpace(name)) names.Add(name);
    }

    private static ReadOnlyDictionary<(long Id, string Version), Overlay> EmptyCache() =>
        new ReadOnlyDictionary<(long, string), Overlay>(new Dictionary<(long, string), Overlay>());
}
