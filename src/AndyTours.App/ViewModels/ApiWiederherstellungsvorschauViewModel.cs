namespace AndyTours.App.ViewModels;

using System.Collections.ObjectModel;
using System.IO;
using AndyTours.Application.Sicherung;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>Metadata preview and separately requested SQLite base inspection; neither grants apply authority.</summary>
public sealed partial class ApiWiederherstellungsvorschauViewModel : ObservableObject, IDisposable
{
    private readonly ISicherungskatalog _katalog;
    private readonly IWiederherstellungsvorschau _vorschauDienst;
    private readonly ISqliteBasispruefung _basispruefung;
    private readonly CancellationTokenSource _lifetime;
    private CancellationTokenSource? _activeCheck;
    private bool _disposed;
    private int _revision;
    [ObservableProperty] private bool _istBeschaeftigt;
    [ObservableProperty] private string _status = "Sicherungen laden, um eine Vorschau anzufordern.";
    [ObservableProperty] private ReadOnlyCollection<SicherungAntwort> _eintraege = Array.AsReadOnly(Array.Empty<SicherungAntwort>());
    [ObservableProperty] private SicherungAntwort? _auswahl;
    [ObservableProperty] private WiederherstellungsvorschauAntwort? _vorschau;
    [ObservableProperty] private SqliteBasispruefungAntwort? _sqliteErgebnis;
    [ObservableProperty] private string _sqliteStatus = "SQLite-Basisprüfung nicht ausgeführt.";
    [ObservableProperty] private bool _mussNeuLaden = true;

    public ApiWiederherstellungsvorschauViewModel(ISicherungskatalog katalog, IWiederherstellungsvorschau vorschauDienst,
        CancellationToken lifetime = default) : this(katalog, vorschauDienst, new DisabledSqliteBasispruefung(), lifetime) { }

    public static ApiWiederherstellungsvorschauViewModel CreateWithDownloadClient(
        ISicherungskatalog katalog, IWiederherstellungsvorschau vorschauDienst,
        ISicherungsdownloadClient downloadClient, CancellationToken lifetime = default) =>
        new(katalog, vorschauDienst, new DisabledSqliteBasispruefung(), downloadClient, lifetime);

    public ApiWiederherstellungsvorschauViewModel(ISicherungskatalog katalog, IWiederherstellungsvorschau vorschauDienst,
        ISqliteBasispruefung basispruefung, CancellationToken lifetime = default)
        : this(katalog, vorschauDienst, basispruefung, null, lifetime) { }

    public ApiWiederherstellungsvorschauViewModel(ISicherungskatalog katalog, IWiederherstellungsvorschau vorschauDienst,
        ISqliteBasispruefung basispruefung, ISicherungsdownloadClient? downloadClient, CancellationToken lifetime = default)
    {
        _katalog = katalog ?? throw new ArgumentNullException(nameof(katalog));
        _vorschauDienst = vorschauDienst ?? throw new ArgumentNullException(nameof(vorschauDienst));
        _basispruefung = basispruefung ?? throw new ArgumentNullException(nameof(basispruefung));
        _downloadClient = downloadClient;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
    }
    private sealed class DisabledSqliteBasispruefung : ISqliteBasispruefung
    {
        public Task<SqliteBasispruefungAntwort> Pruefen(SqliteBasispruefungAnfrage anfrage, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("SQLite-Basisprüfung nicht konfiguriert.");
    }

    private bool CanLaden() => !_disposed && !IstBeschaeftigt;
    private bool CanVorschau() => !_disposed && !IstBeschaeftigt && !MussNeuLaden && Auswahl is not null && Eintraege.Contains(Auswahl);
    private bool CanSqlitePruefen() => CanVorschau() && Auswahl?.Art == Sicherungsart.Vollarchiv;
    partial void OnIstBeschaeftigtChanged(bool value) { NotifyCommands(); OnPropertyChanged(nameof(KannDownloaden)); }
    partial void OnMussNeuLadenChanged(bool value) { VorschauCommand.NotifyCanExecuteChanged(); SqlitePruefenCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(KannDownloaden)); }
    partial void OnAuswahlChanged(SicherungAntwort? value)
    {
        if (_disposed) return;
        _revision++;
        _activeCheck?.Cancel();
        _activeDownload?.Cancel();
        _downloadGeneration++;
        Vorschau = null; SqliteErgebnis = null; SqliteStatus = "SQLite-Basisprüfung nicht ausgeführt.";
        if (value is not null) Status = "Auswahl geändert; Vorschau erneut anfordern.";
        VorschauCommand.NotifyCanExecuteChanged(); SqlitePruefenCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(KannDownloaden));
    }
    [RelayCommand(CanExecute = nameof(CanLaden))]
    public async Task LadenAsync(CancellationToken cancellationToken = default)
    {
        if (!CanLaden()) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        IstBeschaeftigt = true; _revision++; Auswahl = null; Vorschau = null; SqliteErgebnis = null;
        MussNeuLaden = true; Status = "Sicherungskatalog wird geladen.";
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            var response = await _katalog.Liste(linked.Token); linked.Token.ThrowIfCancellationRequested(); if (_disposed) return;
            if (response?.Eintraege is not { } source) throw new InvalidDataException();
            var items = source.ToArray();
            if (items.Any(x => !WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig(x)) || items.Select(x => x.Id).Distinct().Count() != items.Length) throw new InvalidDataException();
            Eintraege = Array.AsReadOnly(items); MussNeuLaden = false;
            Status = Eintraege.Count == 0 ? "Keine Sicherungen verfügbar." : "Sicherung auswählen und Vorschau anfordern.";
        }
        catch (OperationCanceledException) { if (!_disposed) Status = "Laden abgebrochen; bitte erneut laden."; }
        catch (Exception) { if (!_disposed) Status = "Sicherungskatalog ist nicht verfügbar oder ungültig."; }
        finally { if (!_disposed) IstBeschaeftigt = false; }
    }
    [RelayCommand(CanExecute = nameof(CanVorschau))]
    public async Task VorschauAsync(CancellationToken cancellationToken = default)
    {
        if (!CanVorschau() || Auswahl is not { } selected) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        IstBeschaeftigt = true; Vorschau = null; var revision = _revision; var hash = selected.InhaltHash;
        var request = new WiederherstellungsvorschauAnfrage(selected.Id, hash); Status = "Metadatenvorschau wird geladen.";
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            var result = await _vorschauDienst.Pruefen(request, linked.Token); linked.Token.ThrowIfCancellationRequested();
            if (!Current(selected, hash, revision)) return;
            if (!WiederherstellungsvorschauRegeln.IstMetadatenAntwortGueltig(result, request)) { Status = "Vorschau-Antwort ungültig; keine Freigabe."; return; }
            Vorschau = result with { Hinweise = Array.AsReadOnly(result.Hinweise.ToArray()) }; Status = "Metadatenvorschau; keine Anwendungsfreigabe.";
        }
        catch (AndyTours.Application.Stammdaten.StammdatenException ex)
        {
            if (!Current(selected, hash, revision)) return;
            if (ex.Art is AndyTours.Application.Stammdaten.StammdatenFehlerart.NichtGefunden or AndyTours.Application.Stammdaten.StammdatenFehlerart.Konflikt)
            { MussNeuLaden = true; Status = "Sicherungsstand hat sich geändert; Katalog neu laden."; }
            else Status = "Vorschauanforderung ungültig.";
        }
        catch (OperationCanceledException) { if (Current(selected, hash, revision)) Status = "Vorschau abgebrochen."; }
        catch (Exception) { if (Current(selected, hash, revision)) Status = "Vorschau derzeit nicht verfügbar."; }
        finally { if (!_disposed) IstBeschaeftigt = false; }
    }
    [RelayCommand(CanExecute = nameof(CanSqlitePruefen))]
    public async Task SqlitePruefenAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSqlitePruefen() || Auswahl is not { Art: Sicherungsart.Vollarchiv } selected) return;
        var revision = _revision; var hash = selected.InhaltHash;
        using var active = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        _activeCheck?.Cancel();
        _activeCheck = active;
        IstBeschaeftigt = true; SqliteErgebnis = null; SqliteStatus = "SQLite-Basisprüfung läuft (keine Freigabeentscheidung).";
        try
        {
            active.Token.ThrowIfCancellationRequested();
            var result = await _basispruefung.Pruefen(new(selected.Id, hash), active.Token);
            active.Token.ThrowIfCancellationRequested();
            if (!Current(selected, hash, revision)) return;
            if (result is null || result.SicherungsId != selected.Id ||
                result.Status is not ("ok" or "invalid_input" or "readonly_unavailable" or "unsupported_schema" or "core_schema_invalid" or "integrity_failed" or "check_failed") ||
                result.CoreTablesChecked is < 0 or > 8 ||
                result.Status == "ok" && (result.SchemaVersion is null or < 1 or > 5 || result.CoreTablesChecked is null or not 8 || !result.QuickCheck) ||
                result.Hinweise is null || result.Hinweise.Count > 16 || result.Hinweise.Any(h => h is null || h.Length > 256))
            { SqliteStatus = "SQLite-Prüfantwort ungültig; keine Freigabe."; return; }
            SqliteErgebnis = result with { Hinweise = Array.AsReadOnly(result.Hinweise.ToArray()) };
            SqliteStatus = result.Status == "ok"
                ? "SQLite-Basisprüfung erfolgreich; keine Wiederherstellungsfreigabe."
                : "SQLite-Basisprüfung meldet einen Prüfstatus; keine Wiederherstellungsfreigabe.";
        }
        catch (AndyTours.Application.Stammdaten.StammdatenException ex)
        {
            if (!Current(selected, hash, revision)) return;
            if (ex.Art is AndyTours.Application.Stammdaten.StammdatenFehlerart.NichtGefunden or AndyTours.Application.Stammdaten.StammdatenFehlerart.Konflikt)
            { MussNeuLaden = true; SqliteStatus = "Sicherungsstand hat sich geändert; Katalog neu laden."; }
            else SqliteStatus = "SQLite-Prüfanfrage ungültig.";
        }
        catch (OperationCanceledException) { if (Current(selected, hash, revision)) SqliteStatus = "SQLite-Prüfung abgebrochen."; }
        catch (Exception) { if (Current(selected, hash, revision)) SqliteStatus = "SQLite-Prüfung nicht verfügbar oder fehlgeschlagen."; }
        finally
        {
            if (ReferenceEquals(_activeCheck, active)) _activeCheck = null;
            if (!_disposed) IstBeschaeftigt = false;
        }
    }
    private bool Current(SicherungAntwort selected, string hash, int revision) => !_disposed && revision == _revision && Auswahl?.Id == selected.Id && string.Equals(Auswahl.InhaltHash, hash, StringComparison.Ordinal);
    private void NotifyCommands() { LadenCommand.NotifyCanExecuteChanged(); VorschauCommand.NotifyCanExecuteChanged(); SqlitePruefenCommand.NotifyCanExecuteChanged(); }
    public void Dispose() { if (_disposed) return; _disposed = true; _activeCheck?.Cancel(); _activeDownload?.Cancel(); _downloadGeneration++; _lifetime.Cancel(); NotifyCommands(); OnPropertyChanged(nameof(KannDownloaden)); _lifetime.Dispose(); GC.SuppressFinalize(this); }
}
