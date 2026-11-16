using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace AndyTours.App.ViewModels;
public partial class ApiGarminEinstellungenViewModel : ObservableObject, IDisposable
{
    private readonly IGarminEinstellungen _service;
    private readonly CancellationTokenSource _readLifetime=new();
    private string _version=string.Empty;
    private string _keyDraft=string.Empty;
    private bool _closing;
    private bool _disposed;
    [ObservableProperty] private bool _hasKey;
    [ObservableProperty] private bool _allePaketeAutomatisch;
    [ObservableProperty] private bool _autoDraft;
    [ObservableProperty] private bool _laedt;
    [ObservableProperty] private bool _aendert;
    [ObservableProperty] private string _status=string.Empty;
    public ApiGarminEinstellungenViewModel(IGarminEinstellungen service)=>_service=service??throw new ArgumentNullException(nameof(service));
    public event EventHandler? SchluesselEingabeLeerenAngefordert;
    public void SchluesselEntwurfGeaendert(string? value){if(!_closing)_keyDraft=value??string.Empty;}
    partial void OnLaedtChanged(bool value)=>NotifyCommands();
    partial void OnAendertChanged(bool value)=>NotifyCommands();
    partial void OnAutoDraftChanged(bool value)=>AutomatikSpeichernCommand.NotifyCanExecuteChanged();
    private void NotifyCommands(){LadenCommand.NotifyCanExecuteChanged();SchluesselSpeichernCommand.NotifyCanExecuteChanged();SchluesselEntfernenCommand.NotifyCanExecuteChanged();AutomatikSpeichernCommand.NotifyCanExecuteChanged();}
    private bool Idle()=>!_closing&&!Laedt&&!Aendert;
    [RelayCommand(CanExecute=nameof(Idle))]
    public async Task LadenAsync(CancellationToken ct=default)
    {
        if(!Idle())return; using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,_readLifetime.Token); Laedt=true;
        try{var r=await _service.Laden(linked.Token);linked.Token.ThrowIfCancellationRequested();if(_closing)return;Apply(r);Status=string.Empty;}
        catch(OperationCanceledException)when(linked.IsCancellationRequested){}
        catch(StammdatenException e){Status=e.Art==StammdatenFehlerart.Konflikt?"Garmin-Einstellungen wurden geändert; bitte erneut laden.":"Garmin-Einstellungen konnten nicht geladen werden.";}
        catch(Exception){Status="Garmin-Einstellungen konnten nicht geladen werden.";}
        finally{Laedt=false;}
    }
    [RelayCommand(CanExecute=nameof(Idle))]
    private async Task SchluesselSpeichernAsync()
    {
        if(!Idle())return; Aendert=true;
        try{var r=await _service.SchluesselSpeichern(new(_version,_keyDraft));Apply(r);_keyDraft=string.Empty;Status="Garmin-Schlüssel gespeichert.";SchluesselEingabeLeerenAngefordert?.Invoke(this,EventArgs.Empty);}
        catch(StammdatenException e){Status=e.Art==StammdatenFehlerart.Konflikt?"Versionskonflikt. Eingabe bleibt erhalten; bitte neu laden und prüfen.":"Garmin-Schlüssel konnte nicht gespeichert werden.";}
        catch(Exception){Status="Speicherstatus unklar. Bitte neu laden und prüfen; nicht blind erneut speichern.";}
        finally{Aendert=false;}
    }
    [RelayCommand(CanExecute=nameof(Idle))]
    private async Task SchluesselEntfernenAsync()
    {
        if(!Idle())return;Aendert=true;
        try{var r=await _service.SchluesselEntfernen(new(_version));Apply(r);_keyDraft=string.Empty;Status="Garmin-Schlüssel entfernt.";SchluesselEingabeLeerenAngefordert?.Invoke(this,EventArgs.Empty);}
        catch(StammdatenException e){Status=e.Art==StammdatenFehlerart.Konflikt?"Versionskonflikt. Bitte neu laden und prüfen.":"Garmin-Schlüssel konnte nicht entfernt werden.";}
        catch(Exception){Status="Speicherstatus unklar. Bitte neu laden und prüfen; nicht blind erneut speichern.";}
        finally{Aendert=false;}
    }
    [RelayCommand(CanExecute=nameof(CanSaveAuto))]
    private async Task AutomatikSpeichernAsync()
    {
        if(!Idle())return;Aendert=true;
        try{var r=await _service.AutomatikSpeichern(new(_version,AutoDraft));Apply(r);Status="Garmin-Automatik gespeichert.";}
        catch(StammdatenException e){Status=e.Art==StammdatenFehlerart.Konflikt?"Versionskonflikt. Bitte neu laden und prüfen.":"Garmin-Automatik konnte nicht gespeichert werden.";}
        catch(Exception){Status="Speicherstatus unklar. Bitte neu laden und prüfen; nicht blind erneut speichern.";}
        finally{Aendert=false;}
    }
    private bool CanSaveAuto()=>Idle()&&AutoDraft!=AllePaketeAutomatisch;
    private void Apply(GarminEinstellungenAntwort r){_version=r.Version;HasKey=r.HatSchluessel;AllePaketeAutomatisch=r.AllePaketeAutomatisch;AutoDraft=r.AllePaketeAutomatisch;}
    public void BeginClose(){if(_closing)return;_closing=true;_readLifetime.Cancel();NotifyCommands();}
    public void Dispose(){if(_disposed)return;_disposed=true;BeginClose();_readLifetime.Dispose();GC.SuppressFinalize(this);}
}
