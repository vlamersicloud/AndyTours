namespace AndyTours.App.ViewModels;

using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class ApiSicherungseinstellungenViewModel : ObservableObject, IDisposable
{
    private readonly ISicherungskonfiguration _service;
    private readonly CancellationTokenSource _readLifetime = new();
    private string _version = string.Empty;
    private bool _geladen;
    private bool _closing;
    private bool _disposed;
    private bool _reloadRequired;

    [ObservableProperty] private int _intervallEntwurf = 3;
    [ObservableProperty] private int _hoechstzahlEntwurf = 10;
    [ObservableProperty] private int _startsSeitSicherung;
    [ObservableProperty] private bool _laedt;
    [ObservableProperty] private bool _aendert;
    [ObservableProperty] private string _status = string.Empty;

    public ApiSicherungseinstellungenViewModel(ISicherungskonfiguration service) =>
        _service = service ?? throw new ArgumentNullException(nameof(service));

    public bool EingabenGueltig => IntervallEntwurf is >= 1 and <= 99 && HoechstzahlEntwurf is >= 1 and <= 99;
    public bool Bedienbar => !_closing && !Laedt && !Aendert;

    partial void OnIntervallEntwurfChanged(int value) => DraftChanged();
    partial void OnHoechstzahlEntwurfChanged(int value) => DraftChanged();
    partial void OnLaedtChanged(bool value) => NotifyCommands();
    partial void OnAendertChanged(bool value) => NotifyCommands();

    private void DraftChanged()
    {
        OnPropertyChanged(nameof(EingabenGueltig));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(Bedienbar));
        LadenCommand.NotifyCanExecuteChanged();
        SpeichernCommand.NotifyCanExecuteChanged();
    }

    private bool CanRun() => Bedienbar;
    private bool CanSave() => Bedienbar && _geladen && !_reloadRequired && EingabenGueltig;
    private static bool Valid(SicherungskonfigurationAntwort value) =>
        value is not null && value.Version is { Length: 64 } && value.Version.All(Uri.IsHexDigit) &&
        value.Intervall is >= 1 and <= 99 && value.Hoechstzahl is >= 1 and <= 99 && value.StartsSeitSicherung >= 0;

    [RelayCommand(CanExecute = nameof(CanRun))]
    public async Task LadenAsync(CancellationToken ct = default)
    {
        if (!CanRun()) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _readLifetime.Token);
        _reloadRequired = true;
        Laedt = true;
        try
        {
            var response = await _service.Laden(linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (_closing) return;
            if (!Valid(response)) { Status = "Sicherungseinstellungen sind ungültig."; return; }
            Apply(response);
            _geladen = true;
            _reloadRequired = false;
            Status = string.Empty;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (Exception)
        {
            if (!_closing) Status = "Sicherungseinstellungen konnten nicht geladen werden.";
        }
        finally { Laedt = false; }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    public async Task SpeichernAsync(CancellationToken ct = default)
    {
        if (!CanSave()) return;
        var version = _version;
        var intervall = IntervallEntwurf;
        var hoechstzahl = HoechstzahlEntwurf;
        var request = new SicherungskonfigurationAnfrage(version, intervall, hoechstzahl);
        Aendert = true;
        try
        {
            var response = await _service.Speichern(request, ct);
            if (_closing) return;
            if (!Valid(response)) { _reloadRequired = true; Status = "Speicherstatus unklar. Bitte neu laden und prüfen; nicht blind erneut speichern."; return; }
            Apply(response);
            _geladen = true;
            _reloadRequired = false;
            Status = "Sicherungseinstellungen gespeichert.";
        }
        catch (StammdatenException e)
        {
            if (!_closing)
            {
                if (e.Art == StammdatenFehlerart.Konflikt) _reloadRequired = true;
                Status = e.Art == StammdatenFehlerart.Konflikt
                    ? "Versionskonflikt. Entwürfe bleiben erhalten; bitte neu laden und prüfen."
                    : "Sicherungseinstellungen konnten nicht gespeichert werden.";
            }
        }
        catch (Exception)
        {
            if (!_closing)
            {
                _reloadRequired = true;
                Status = "Speicherstatus unklar. Bitte neu laden und prüfen; nicht blind erneut speichern.";
            }
        }
        finally { Aendert = false; }
    }

    private void Apply(SicherungskonfigurationAntwort response)
    {
        _version = response.Version;
        StartsSeitSicherung = response.StartsSeitSicherung;
        IntervallEntwurf = response.Intervall;
        HoechstzahlEntwurf = response.Hoechstzahl;
        OnPropertyChanged(nameof(EingabenGueltig));
        NotifyCommands();
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
        _readLifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
