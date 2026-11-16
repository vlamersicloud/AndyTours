using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

public partial class ApiGarminEinstellungenViewModel : ObservableObject, IDisposable
{
    private readonly IGarminEinstellungen _service;
    private readonly CancellationTokenSource _readLifetime = new();
    private string _version = string.Empty;
    private string _keyDraft = string.Empty;
    private bool _geladen;
    private bool _closing;
    private bool _disposed;

    [ObservableProperty] private bool _hasKey;
    [ObservableProperty] private bool _allePaketeAutomatisch;
    [ObservableProperty] private bool _autoDraft;
    [ObservableProperty] private bool _laedt;
    [ObservableProperty] private bool _aendert;
    [ObservableProperty] private string _status = string.Empty;

    public ApiGarminEinstellungenViewModel(IGarminEinstellungen service) =>
        _service = service ?? throw new ArgumentNullException(nameof(service));

    public event EventHandler? SchluesselEingabeLeerenAngefordert;

    public void SchluesselEntwurfGeaendert(string? value)
    {
        if (!Idle()) return;
        _keyDraft = value ?? string.Empty;
        Status = string.IsNullOrEmpty(_keyDraft) ? string.Empty : "Nicht gespeichert.";
        SchluesselSpeichernCommand.NotifyCanExecuteChanged();
    }

    partial void OnLaedtChanged(bool value) => NotifyCommands();
    partial void OnAendertChanged(bool value) => NotifyCommands();
    partial void OnAutoDraftChanged(bool value) => AutomatikSpeichernCommand.NotifyCanExecuteChanged();

    private void NotifyCommands()
    {
        LadenCommand.NotifyCanExecuteChanged();
        SchluesselSpeichernCommand.NotifyCanExecuteChanged();
        SchluesselEntfernenCommand.NotifyCanExecuteChanged();
        AutomatikSpeichernCommand.NotifyCanExecuteChanged();
    }

    private bool Idle() => !_closing && !Laedt && !Aendert;
    private bool CanMutate() => Idle() && _geladen;
    private bool CanSaveKey()
    {
        if (!CanMutate()) return false;
        var trimmed = _keyDraft.Trim();
        return trimmed.Length is > 0 and <= 4096 &&
               trimmed.All(c => char.IsAsciiLetterOrDigit(c) || "-._~+/=".Contains(c));
    }
    private bool CanSaveAuto() => CanMutate() && AutoDraft != AllePaketeAutomatisch;

    [RelayCommand(CanExecute = nameof(Idle))]
    public async Task LadenAsync(CancellationToken ct = default)
    {
        if (!Idle()) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _readLifetime.Token);
        Laedt = true;
        try
        {
            var response = await _service.Laden(linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (_closing) return;
            Apply(response, resetAutoDraft: true);
            _geladen = true;
            NotifyCommands();
            Status = string.Empty;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (Exception) { Status = "Garmin-Einstellungen konnten nicht geladen werden."; }
        finally { Laedt = false; }
    }

    [RelayCommand(CanExecute = nameof(CanSaveKey))]
    private async Task SchluesselSpeichernAsync()
    {
        if (!CanSaveKey()) return;
        bool preserveAutoDraft = AutoDraft != AllePaketeAutomatisch;
        bool autoDraft = AutoDraft;
        Aendert = true;
        try
        {
            var response = await _service.SchluesselSpeichern(new(_version, _keyDraft));
            Apply(response, resetAutoDraft: !preserveAutoDraft);
            _keyDraft = string.Empty;
            Status = "Garmin-Schlüssel gespeichert.";
            SchluesselEingabeLeerenAngefordert?.Invoke(this, EventArgs.Empty);
            if (preserveAutoDraft) AutoDraft = autoDraft;
        }
        catch (StammdatenException e)
        {
            Status = e.Art == StammdatenFehlerart.Konflikt
                ? "Versionskonflikt. Eingabe bleibt erhalten; bitte neu laden und prüfen."
                : "Garmin-Schlüssel konnte nicht gespeichert werden.";
        }
        catch (Exception) { Status = "Speicherstatus unklar. Bitte neu laden und prüfen; nicht blind erneut speichern."; }
        finally { Aendert = false; }
    }

    [RelayCommand(CanExecute = nameof(CanMutate))]
    private async Task SchluesselEntfernenAsync()
    {
        if (!CanMutate()) return;
        bool preserveAutoDraft = AutoDraft != AllePaketeAutomatisch;
        bool autoDraft = AutoDraft;
        Aendert = true;
        try
        {
            var response = await _service.SchluesselEntfernen(new(_version));
            Apply(response, resetAutoDraft: !preserveAutoDraft);
            _keyDraft = string.Empty;
            Status = "Garmin-Schlüssel entfernt.";
            SchluesselEingabeLeerenAngefordert?.Invoke(this, EventArgs.Empty);
            if (preserveAutoDraft) AutoDraft = autoDraft;
        }
        catch (StammdatenException e)
        {
            Status = e.Art == StammdatenFehlerart.Konflikt
                ? "Versionskonflikt. Bitte neu laden und prüfen."
                : "Garmin-Schlüssel konnte nicht entfernt werden.";
        }
        catch (Exception) { Status = "Speicherstatus unklar. Bitte neu laden und prüfen; nicht blind erneut speichern."; }
        finally { Aendert = false; }
    }

    [RelayCommand(CanExecute = nameof(CanSaveAuto))]
    private async Task AutomatikSpeichernAsync()
    {
        if (!CanSaveAuto()) return;
        Aendert = true;
        try
        {
            var response = await _service.AutomatikSpeichern(new(_version, AutoDraft));
            Apply(response, resetAutoDraft: true);
            Status = "Garmin-Automatik gespeichert.";
        }
        catch (StammdatenException e)
        {
            Status = e.Art == StammdatenFehlerart.Konflikt
                ? "Versionskonflikt. Bitte neu laden und prüfen."
                : "Garmin-Automatik konnte nicht gespeichert werden.";
        }
        catch (Exception) { Status = "Speicherstatus unklar. Bitte neu laden und prüfen; nicht blind erneut speichern."; }
        finally { Aendert = false; }
    }

    private void Apply(GarminEinstellungenAntwort response, bool resetAutoDraft)
    {
        _version = response.Version;
        HasKey = response.HatSchluessel;
        AllePaketeAutomatisch = response.AllePaketeAutomatisch;
        if (resetAutoDraft) AutoDraft = response.AllePaketeAutomatisch;
    }

    public void BeginClose()
    {
        if (_closing) return;
        _closing = true;
        _readLifetime.Cancel();
        NotifyCommands();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        BeginClose();
        _keyDraft = string.Empty;
        _readLifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
