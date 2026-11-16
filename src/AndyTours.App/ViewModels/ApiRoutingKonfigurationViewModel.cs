using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

/// <summary>Read-only foundation for the API routing settings window; setters never persist.</summary>
public partial class ApiRoutingKonfigurationViewModel : ObservableObject, IDisposable
{
    private readonly IRoutingKonfiguration _service;
    private readonly CancellationToken _lifetimeToken;
    private CancellationTokenSource? _operation;
    private string _version = string.Empty;
    private bool _loaded;
    private RoutingAnbieter _confirmedDienst;
    private string? _gespeicherteAdresse;
    private bool _disposed;
    private bool _hatUnklarenSpeicherstatus;

    [ObservableProperty] private RoutingAnbieter _dienst;
    [ObservableProperty] private string _adresseEntwurf = string.Empty;
    [ObservableProperty] private string _orsSchluesselEntwurf = string.Empty;
    [ObservableProperty] private string _hinweis = "Einstellungen wurden noch nicht geladen.";
    [ObservableProperty] private bool _laedt;
    [ObservableProperty] private bool _aendert;
    [ObservableProperty] private bool _adresseAusgeblendet;
    [ObservableProperty] private bool _hatOrsSchluessel;

    public ApiRoutingKonfigurationViewModel(IRoutingKonfiguration service, CancellationToken lifetimeToken = default)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _lifetimeToken = lifetimeToken;
    }

    public IReadOnlyList<RoutingAnbieter> DienstOptionen { get; } = Enum.GetValues<RoutingAnbieter>();
    public bool StatusGeladen => _loaded && !_disposed;
    public bool HatUnklarenSpeicherstatus => _hatUnklarenSpeicherstatus;
    public bool Busy => Laedt || Aendert;
    public bool KannBearbeiten => StatusGeladen && !Busy;
    public string? GespeicherteAdresse => _gespeicherteAdresse;
    public bool HatEntwuerfe => AdresseEntwurf.Length > 0 || OrsSchluesselEntwurf.Length > 0 || (_loaded && Dienst != _confirmedDienst);
    public Func<string, bool>? Bestaetigung { get; set; }
    public event EventHandler? EntwurfLeeren;

    partial void OnLaedtChanged(bool value) => NotifyState();
    partial void OnAendertChanged(bool value) => NotifyState();
    partial void OnDienstChanged(RoutingAnbieter value) => OnPropertyChanged(nameof(HatEntwuerfe));
    partial void OnAdresseEntwurfChanged(string value) => OnPropertyChanged(nameof(HatEntwuerfe));
    partial void OnOrsSchluesselEntwurfChanged(string value) => OnPropertyChanged(nameof(HatEntwuerfe));

    private void NotifyState()
    {
        OnPropertyChanged(nameof(Busy));
        OnPropertyChanged(nameof(KannBearbeiten));
        OnPropertyChanged(nameof(StatusGeladen));
        OnPropertyChanged(nameof(AdresseStatus));
        OnPropertyChanged(nameof(OrsSchluesselStatus));
        LadenCommand.NotifyCanExecuteChanged();
        AbbrechenCommand.NotifyCanExecuteChanged();
        DienstSpeichernCommand.NotifyCanExecuteChanged();
        AdresseSpeichernCommand.NotifyCanExecuteChanged();
        AdresseZuruecksetzenCommand.NotifyCanExecuteChanged();
        OrsSchluesselSpeichernCommand.NotifyCanExecuteChanged();
        OrsSchluesselEntfernenCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private async Task LadenAsync(CancellationToken cancellationToken)
    {
        if (!CanLoad()) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        _operation = linked;
        Laedt = true;
        try
        {
            var response = await _service.Laden(linked.Token).ConfigureAwait(true);
            linked.Token.ThrowIfCancellationRequested();
            if (_disposed) return;
            if (response is null || response.Version is not { Length: 64 } version || !version.All(Uri.IsHexDigit) ||
                !Enum.IsDefined(response.Dienst) || response.AdresseAusgeblendet && response.BRouterAdresse is not null ||
                !response.AdresseAusgeblendet && response.BRouterAdresse is null ||
                response.BRouterAdresse is not null && !RoutingKonfigurationRegeln.IstAdresseSichtbar(response.BRouterAdresse))
                throw new InvalidOperationException();
            bool preserveDienstDraft = _loaded && Dienst != _confirmedDienst;
            _version = version;
            _confirmedDienst = response.Dienst;
            if (!preserveDienstDraft) Dienst = response.Dienst;
            _gespeicherteAdresse = response.BRouterAdresse;
            OnPropertyChanged(nameof(GespeicherteAdresse));
            OnPropertyChanged(nameof(HatEntwuerfe));
            AdresseAusgeblendet = response.AdresseAusgeblendet;
            HatOrsSchluessel = response.HatOrsSchluessel;
            // Preserve edits: GET refreshes authoritative metadata, never stomps in-progress user drafts.
            _loaded = true;
            _hatUnklarenSpeicherstatus = false;
            OnPropertyChanged(nameof(HatUnklarenSpeicherstatus));
            Hinweis = response.AdresseAusgeblendet
                ? "Gespeicherte BRouter-Adresse ist verborgen. Ersetzen oder zurücksetzen."
                : "Konfiguration geladen.";
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (Exception) { if (!_disposed) Hinweis = "Routingkonfiguration konnte nicht geladen werden."; }
        finally
        {
            if (ReferenceEquals(_operation, linked)) _operation = null;
            if (!_disposed) Laedt = false;
        }
    }

    private bool CanLoad() => !_disposed && !Busy;

    [RelayCommand(CanExecute = nameof(CanAbort))]
    public void Abbrechen() { try { _operation?.Cancel(); } catch (ObjectDisposedException) { } }
    private bool CanAbort() => !_disposed && _operation is not null;

    public void EntwurfLeerenAnfordern()
    {
        if (_disposed) return;
        OrsSchluesselEntwurf = string.Empty;
        EntwurfLeeren?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _operation?.Cancel(); } catch (ObjectDisposedException) { }
        _operation = null;
        _version = string.Empty;
        _gespeicherteAdresse = null;
        Laedt = false;
        Aendert = false;
        AdresseAusgeblendet = false;
        HatOrsSchluessel = false;
        OnPropertyChanged(nameof(GespeicherteAdresse));
        OnPropertyChanged(nameof(HatEntwuerfe));
        OrsSchluesselEntwurf = string.Empty;
        AdresseEntwurf = string.Empty;
        _loaded = false;
        EntwurfLeeren = null;
        Bestaetigung = null;
        NotifyState();
        GC.SuppressFinalize(this);
    }
}
