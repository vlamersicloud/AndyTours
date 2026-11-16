using AndyTours.Application.Planung;
using CommunityToolkit.Mvvm.Input;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public partial class ApiRoutingKonfigurationViewModel
{
    public string OrsSchluesselStatus => !_loaded ? "Schlüsselstatus unbekannt (Einstellungen noch nicht geladen)." :
        HatOrsSchluessel ? "Ein ORS-Schlüssel ist gespeichert." : "Kein ORS-Schlüssel gespeichert.";
    public string AdresseStatus => !_loaded ? "Adressstatus unbekannt (Einstellungen noch nicht geladen)." :
        AdresseAusgeblendet ? "Gespeicherte Adresse verborgen; sie bleibt unverändert, bis Sie sie ersetzen oder zurücksetzen." :
        "Gespeicherte Adresse wird angezeigt.";

    partial void OnHatOrsSchluesselChanged(bool value) => OnPropertyChanged(nameof(OrsSchluesselStatus));
    partial void OnAdresseAusgeblendetChanged(bool value) => OnPropertyChanged(nameof(AdresseStatus));

    private bool CanWrite() => !_disposed && _loaded && !Busy;

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private Task DienstSpeichernAsync() => RunWrite(
        (version, token) => _service.DienstSpeichern(new(version, Dienst), token), null, "Routinganbieter gespeichert.");

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private Task AdresseSpeichernAsync()
    {
        if (!CanWrite()) return Task.CompletedTask;
        string draft = AdresseEntwurf;
        try { _ = RoutingKonfigurationRegeln.Adresse(draft); }
        catch (StammdatenException) { Hinweis = "BRouter-Adresse ungültig."; return Task.CompletedTask; }
        return RunWrite((version, token) => _service.AdresseSpeichern(new(version, draft), token),
            () => { if (AdresseEntwurf == draft) AdresseEntwurf = string.Empty; }, "BRouter-Adresse gespeichert.");
    }

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private Task AdresseZuruecksetzenAsync()
    {
        if (!CanWrite()) return Task.CompletedTask;
        if (Bestaetigung?.Invoke("Gespeicherte BRouter-Adresse zurücksetzen?") != true) return Task.CompletedTask;
        return RunWrite((version, token) => _service.AdresseZuruecksetzen(new(version), token), null, "BRouter-Adresse zurückgesetzt.");
    }

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private Task OrsSchluesselSpeichernAsync()
    {
        if (!CanWrite()) return Task.CompletedTask;
        string draft = OrsSchluesselEntwurf;
        try { _ = RoutingKonfigurationRegeln.OrsSchluessel(draft); }
        catch (StammdatenException) { Hinweis = "ORS-Schlüssel ungültig."; return Task.CompletedTask; }
        return RunWrite((version, token) => _service.OrsKeySpeichern(new(version, draft), token),
            () => { if (OrsSchluesselEntwurf == draft) { OrsSchluesselEntwurf = string.Empty; EntwurfLeeren?.Invoke(this, EventArgs.Empty); } },
            "ORS-Schlüssel gespeichert.");
    }

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private Task OrsSchluesselEntfernenAsync()
    {
        if (!CanWrite()) return Task.CompletedTask;
        if (Bestaetigung?.Invoke("Gespeicherten ORS-Schlüssel entfernen?") != true) return Task.CompletedTask;
        return RunWrite((version, token) => _service.OrsKeyEntfernen(new(version), token),
            null, "ORS-Schlüssel entfernt.");
    }

    private async Task RunWrite(Func<string, CancellationToken, Task<RoutingKonfigurationAntwort>> send, Action? confirmedDraftClear, string success)
    {
        if (!CanWrite()) return;
        string version = _version;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken);
        _operation = linked;
        Aendert = true;
        try
        {
            var response = await send(version, linked.Token).ConfigureAwait(true);
            if (_disposed) return;
            // A valid server response confirms the committed write even if cancellation raced afterward.
            ApplyPublicMetadata(response);
            confirmedDraftClear?.Invoke();
            Hinweis = success;
        }
        catch (StammdatenException e)
        {
            if (!_disposed)
            {
                if (e.Art == StammdatenFehlerart.Konflikt) Invalidate("Versionskonflikt. Entwurf bleibt erhalten; bitte Einstellungen ausdrücklich neu laden.");
                else Hinweis = "Eingabe ungültig; Entwurf bleibt erhalten.";
            }
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            if (!_disposed) Invalidate("Speicherstatus unklar. Entwurf bleibt erhalten; bitte ausdrücklich neu laden.", uncertain: true);
        }
        catch
        {
            if (!_disposed) Invalidate("Speicherstatus unklar. Entwurf bleibt erhalten; bitte ausdrücklich neu laden.", uncertain: true);
        }
        finally
        {
            if (ReferenceEquals(_operation, linked)) _operation = null;
            if (!_disposed) Aendert = false;
        }
    }

    private void ApplyPublicMetadata(RoutingKonfigurationAntwort response)
    {
        if (response is null) throw new InvalidOperationException();
        string version = response.Version;
        if (version is null || version.Length != 64 || !version.All(Uri.IsHexDigit) ||
            !Enum.IsDefined(response.Dienst) || response.AdresseAusgeblendet && response.BRouterAdresse is not null ||
            !response.AdresseAusgeblendet && response.BRouterAdresse is null ||
            response.BRouterAdresse is not null && !RoutingKonfigurationRegeln.IstAdresseSichtbar(response.BRouterAdresse))
            throw new InvalidOperationException();
        bool preserveDienstDraft = _loaded && Dienst != _confirmedDienst;
        _version = version;
        _loaded = true;
        _hatUnklarenSpeicherstatus = false;
        OnPropertyChanged(nameof(HatUnklarenSpeicherstatus));
        _confirmedDienst = response.Dienst;
        if (!preserveDienstDraft) Dienst = response.Dienst;
        _gespeicherteAdresse = response.BRouterAdresse;
        OnPropertyChanged(nameof(GespeicherteAdresse));
        OnPropertyChanged(nameof(HatEntwuerfe));
        AdresseAusgeblendet = response.AdresseAusgeblendet;
        HatOrsSchluessel = response.HatOrsSchluessel;
    }

    private void Invalidate(string message, bool uncertain = false)
    {
        _loaded = false;
        _hatUnklarenSpeicherstatus = uncertain;
        OnPropertyChanged(nameof(HatUnklarenSpeicherstatus));
        _version = string.Empty;
        Hinweis = message;
        OnPropertyChanged(nameof(StatusGeladen));
        OnPropertyChanged(nameof(KannBearbeiten));
        OnPropertyChanged(nameof(OrsSchluesselStatus));
        OnPropertyChanged(nameof(AdresseStatus));
        NotifyState();
    }
}
