using System.Collections.ObjectModel;
using System.Globalization;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

/// <summary>Serverbasierte Wartungs- und Alarmverwaltung für die API-Oberfläche.</summary>
public partial class ApiWartungsViewModel : ObservableObject
{
    private readonly IWartungsverwaltung _wartungen;
    private readonly IStammdatenLeser _stammdaten;
    private readonly Func<string, bool> _bestaetigen;
    private WartungsbestandAntwort? _bestand;
    private StammdatenAntwort? _meta;
    private bool _filterSuspendiert;
    private bool _erstesLaden = true;
    private bool _hasLoaded;
    private long _generation;

    [ObservableProperty] private ApiFilterOption? _aktivitaetAuswahl;
    [ObservableProperty] private ApiFilterOption? _equipmentAuswahl;
    [ObservableProperty] private bool _laedt;
    [ObservableProperty] private bool _aendert;
    [ObservableProperty] private bool _editorOffen;
    [ObservableProperty] private bool _hatAenderungen;
    [ObservableProperty] private string _status = "Wartungsdaten laden.";
    [ObservableProperty] private IReadOnlyList<long> _wartungIds = Array.Empty<long>();
    [ObservableProperty] private IReadOnlyList<long> _alarmIds = Array.Empty<long>();

    public ApiWartungsViewModel(IWartungsverwaltung wartungen, IStammdatenLeser stammdaten,
        Func<string, bool>? bestaetigen = null)
    {
        _wartungen = wartungen;
        _stammdaten = stammdaten;
        _bestaetigen = bestaetigen ?? (_ => false);
        AktivitaetsOptionen = [new(null, "Alle Aktivitäten")];
        EquipmentOptionen = [new(null, "Alle Equipments")];
        Wartungszeilen = [];
        Alarmzeilen = [];
        Taetigkeiten = [];
    }

    public ObservableCollection<ApiFilterOption> AktivitaetsOptionen { get; }
    public ObservableCollection<ApiFilterOption> EquipmentOptionen { get; }
    public ObservableCollection<WartungszeileAntwort> Wartungszeilen { get; }
    public ObservableCollection<AlarmzeileAntwort> Alarmzeilen { get; }
    public IReadOnlyList<string> Taetigkeiten { get; private set; }
    public event EventHandler<WartungBearbeitenViewModel>? WartungEditorAngefordert;
    public event EventHandler<AlarmBearbeitenViewModel>? AlarmEditorAngefordert;

    partial void OnAktivitaetAuswahlChanged(ApiFilterOption? value)
    {
        if (_filterSuspendiert) return;
        UpdateEquipmentOptions();
        FilterChanged();
    }
    partial void OnEquipmentAuswahlChanged(ApiFilterOption? value)
    {
        if (!_filterSuspendiert) FilterChanged();
    }
    partial void OnLaedtChanged(bool value) => NotifyCommands();
    partial void OnAendertChanged(bool value) => NotifyCommands();
    partial void OnEditorOffenChanged(bool value) => NotifyCommands();
    partial void OnWartungIdsChanged(IReadOnlyList<long> value) => NotifyCommands();
    partial void OnAlarmIdsChanged(IReadOnlyList<long> value) => NotifyCommands();

    private void NotifyCommands()
    {
        LadenCommand.NotifyCanExecuteChanged(); NeueWartungCommand.NotifyCanExecuteChanged();
        WartungBearbeitenCommand.NotifyCanExecuteChanged(); NeuerAlarmCommand.NotifyCanExecuteChanged();
        AlarmBearbeitenCommand.NotifyCanExecuteChanged(); WartungenLoeschenCommand.NotifyCanExecuteChanged();
        AlarmeLoeschenCommand.NotifyCanExecuteChanged(); AlarmeUmschaltenCommand.NotifyCanExecuteChanged();
    }

    private void FilterChanged()
    {
        Interlocked.Increment(ref _generation);
        Wartungszeilen.Clear(); Alarmzeilen.Clear(); WartungIds = []; AlarmIds = [];
        _hasLoaded = false; _bestand = null;
        HatAenderungen = true; Status = "Filter geändert – bitte neu laden.";
    }

    [RelayCommand(CanExecute = nameof(CanLoad))]
    public Task LadenAsync(CancellationToken ct = default) => LoadCore(ct);
    private bool CanLoad() => !Laedt && !Aendert && !EditorOffen;

    private async Task LoadCore(CancellationToken ct)
    {
        if (Laedt) return;
        var generation = Interlocked.Increment(ref _generation);
        Laedt = true;
        try
        {
            var meta = await _stammdaten.Laden(ct);
            var previousActivity = AktivitaetAuswahl?.Id;
            var previousEquipment = EquipmentAuswahl?.Id;
            var activity = meta.Aktivitaeten.FirstOrDefault(a => a.Id == previousActivity);
            if (activity is null && _erstesLaden)
                activity = meta.Aktivitaeten.FirstOrDefault(a => a.IstHaupt);
            if (activity is null && previousActivity is not null)
                Status = "Die zuvor gewählte Aktivität ist nicht mehr verfügbar; alle Aktivitäten werden angezeigt.";

            _filterSuspendiert = true;
            try
            {
                _meta = meta;
                AktivitaetsOptionen.Clear(); AktivitaetsOptionen.Add(new(null, "Alle Aktivitäten"));
                foreach (var a in meta.Aktivitaeten) AktivitaetsOptionen.Add(new(a.Id, a.Name));
                AktivitaetAuswahl = activity is null ? AktivitaetsOptionen[0] : AktivitaetsOptionen.First(x => x.Id == activity.Id);
                UpdateEquipmentOptions();
                EquipmentAuswahl = EquipmentOptionen.FirstOrDefault(x => x.Id == previousEquipment) ?? EquipmentOptionen[0];
            }
            finally { _filterSuspendiert = false; }
            _erstesLaden = false;
            var overview = await _wartungen.Laden(new(AktivitaetAuswahl?.Id, EquipmentAuswahl?.Id), ct);
            if (generation != Interlocked.Read(ref _generation)) return;
            _bestand = overview.Bestand;
            Taetigkeiten = overview.Taetigkeiten; OnPropertyChanged(nameof(Taetigkeiten));
            Wartungszeilen.Clear(); foreach (var row in overview.Wartungszeilen) Wartungszeilen.Add(row);
            Alarmzeilen.Clear(); foreach (var row in overview.Alarmzeilen) Alarmzeilen.Add(row);
            _hasLoaded = true; HatAenderungen = false;
            if (string.IsNullOrEmpty(Status) || Status == "Filter geändert – bitte neu laden.") Status = string.Empty;
        }
        catch (Exception) { if (generation == Interlocked.Read(ref _generation)) Status = "Wartungsdaten konnten nicht geladen werden."; }
        finally { Laedt = false; NotifyCommands(); }
    }

    private void UpdateEquipmentOptions()
    {
        if (_meta is null) return;
        var selected = EquipmentAuswahl?.Id;
        EquipmentOptionen.Clear(); EquipmentOptionen.Add(new(null, "Alle Equipments"));
        foreach (var e in _meta.Equipments.Where(e => !e.Ausgemustert && (AktivitaetAuswahl?.Id is not long aid || e.AktivitaetId == aid)))
            EquipmentOptionen.Add(new(e.Id, e.Name));
        if (!EquipmentOptionen.Any(x => x.Id == selected)) EquipmentAuswahl = EquipmentOptionen[0];
    }

    [RelayCommand(CanExecute = nameof(CanNew))] private Task NeueWartungAsync() => EditMaintenance(null);
    [RelayCommand(CanExecute = nameof(CanEditMaintenance))] private Task WartungBearbeitenAsync() => WartungIds.Count == 1 ? EditMaintenance(WartungIds[0]) : Task.CompletedTask;
    [RelayCommand(CanExecute = nameof(CanNew))] private Task NeuerAlarmAsync() => EditAlarm(null);
    [RelayCommand(CanExecute = nameof(CanEditAlarm))] private Task AlarmBearbeitenAsync() => AlarmIds.Count == 1 ? EditAlarm(AlarmIds[0]) : Task.CompletedTask;
    private bool CanNew() => IsIdleWithSnapshot();
    private bool CanEditMaintenance() => IsIdleWithSnapshot() && WartungIds.Count == 1;
    private bool CanEditAlarm() => IsIdleWithSnapshot() && AlarmIds.Count == 1;
    private bool IsIdleWithSnapshot() => !Laedt && !Aendert && !EditorOffen && _hasLoaded && _bestand is not null && _meta is not null;

    private async Task EditMaintenance(long? id)
    {
        var loaded = await LoadEditorSnapshot(); if (loaded is null) return;
        var snapshot = loaded.Value.Bestand; var meta = loaded.Value.Meta;
        Wartung? existing = id is long selected ? snapshot.Eintraege.FirstOrDefault(x => x.Id == selected && x.Art == Wartungsart.Wartung) is { } item ? ToCore(item) : null : null;
        if (id is not null && existing is null) { Status = "Die ausgewählte Wartung ist nicht mehr vorhanden. Bitte neu laden."; return; }
        var basis = existing ?? new Wartung { Datum = DateOnly.FromDateTime(DateTime.Today), EquipmentId = EquipmentAuswahl?.Id, Taetigkeit = "", Typ = WartungsTyp.Wartung, Aktiv = true };
        var data = CoreData(meta, snapshot);
        var vm = new WartungBearbeitenViewModel(basis, ErmittleWartungsraeder.Ausfuehren(data, AktivitaetAuswahl?.Id, existing?.EquipmentId), Taetigkeiten, id is null ? "Neue Wartung" : "Wartung bearbeiten", async value => await SaveMaintenance(value, snapshot.Version), DateOnly.FromDateTime(DateTime.Today), data);
        EditorOffen = true; try { WartungEditorAngefordert?.Invoke(this, vm); } finally { EditorOffen = false; }
        if (vm.IstGespeichert) await ReloadAfterSave();
    }

    private async Task EditAlarm(long? id)
    {
        var loaded = await LoadEditorSnapshot(); if (loaded is null) return;
        var snapshot = loaded.Value.Bestand; var meta = loaded.Value.Meta;
        Wartung? existing = id is long selected ? snapshot.Eintraege.FirstOrDefault(x => x.Id == selected && x.Art == Wartungsart.Alarm) is { } item ? ToCore(item) : null : null;
        if (id is not null && existing is null) { Status = "Der ausgewählte Alarm ist nicht mehr vorhanden. Bitte neu laden."; return; }
        var basis = existing ?? new Wartung { Datum = DateOnly.FromDateTime(DateTime.Today), EquipmentId = EquipmentAuswahl?.Id, Taetigkeit = "", Typ = WartungsTyp.Alarm, AlarmKm = 5000, Aktiv = true };
        var data = CoreData(meta, snapshot);
        var vm = new AlarmBearbeitenViewModel(basis, ErmittleWartungsraeder.Ausfuehren(data, AktivitaetAuswahl?.Id, existing?.EquipmentId), Taetigkeiten, id is null ? "Neuer Alarm" : "Alarm bearbeiten", async value => await SaveAlarm(value, snapshot.Version), data);
        EditorOffen = true; try { AlarmEditorAngefordert?.Invoke(this, vm); } finally { EditorOffen = false; }
        if (vm.IstGespeichert) await ReloadAfterSave();
    }

    private async Task<(WartungsbestandAntwort Bestand, StammdatenAntwort Meta)?> LoadEditorSnapshot()
    {
        if (Laedt || Aendert || EditorOffen) return null;
        Laedt = true;
        try
        {
            var mt = _stammdaten.Laden(); var wt = _wartungen.Laden(new(AktivitaetAuswahl?.Id, EquipmentAuswahl?.Id));
            await Task.WhenAll(mt, wt); _meta = await mt; _bestand = (await wt).Bestand; Taetigkeiten = (await wt).Taetigkeiten;
            return (_bestand, _meta);
        }
        catch (Exception) { Status = "Aktuelle Daten für den Editor konnten nicht geladen werden."; return null; }
        finally { Laedt = false; }
    }

    private async Task<bool> SaveMaintenance(Wartung item, string version)
    {
        Aendert = true;
        try
        {
            var response = item.Id == 0
                ? await _wartungen.WartungAnlegen(new(version, item.Datum, item.EquipmentId, item.Taetigkeit, item.Kommentar))
                : await _wartungen.WartungAendern(item.Id, new(version, item.Datum, item.EquipmentId, item.Taetigkeit, item.Kommentar));
            _bestand = response; return true;
        }
        catch (StammdatenException e) { Status = e.Message; return false; }
        catch (Exception) { Status = "Speicherstatus unklar. Bitte vor einem erneuten Versuch den Bestand prüfen."; return false; }
        finally { Aendert = false; }
    }

    private async Task<bool> SaveAlarm(Wartung item, string version)
    {
        if (!int.TryParse(item.AlarmKm.ToString(CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval)) return false;
        Aendert = true;
        try
        {
            var response = item.Id == 0
                ? await _wartungen.AlarmAnlegen(new(version, item.EquipmentId ?? 0, item.Taetigkeit, interval, item.Aktiv))
                : await _wartungen.AlarmAendern(item.Id, new(version, item.EquipmentId ?? 0, item.Taetigkeit, interval, item.Aktiv));
            _bestand = response; return true;
        }
        catch (StammdatenException e) { Status = e.Message; return false; }
        catch (Exception) { Status = "Speicherstatus unklar. Bitte vor einem erneuten Versuch den Bestand prüfen."; return false; }
        finally { Aendert = false; }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteMaintenance))] private Task WartungenLoeschenAsync() => BulkDelete(WartungIds);
    [RelayCommand(CanExecute = nameof(CanDeleteAlarm))] private Task AlarmeLoeschenAsync() => BulkDelete(AlarmIds);
    private async Task BulkDelete(IReadOnlyList<long> ids)
    {
        if (_bestand is null || ids.Count == 0 || !_bestaetigen("Ausgewählte Einträge löschen?")) return;
        Aendert = true; try { _bestand = await _wartungen.Loeschen(new(_bestand.Version, ids)); await ReloadAfterSave(); } catch (Exception) { Status = "Löschen fehlgeschlagen oder Status unklar; bitte neu laden."; } finally { Aendert = false; }
    }
    [RelayCommand(CanExecute = nameof(CanToggleAlarm))] private async Task AlarmeUmschaltenAsync()
    {
        if (_bestand is null || AlarmIds.Count == 0) return;
        Aendert = true; try { _bestand = await _wartungen.AlarmeUmschalten(new(_bestand.Version, AlarmIds)); await ReloadAfterSave(); } catch (Exception) { Status = "Umschalten fehlgeschlagen oder Status unklar; bitte neu laden."; } finally { Aendert = false; }
    }
    private bool CanBulk() => !Laedt && !Aendert && !EditorOffen && _hasLoaded && _bestand is not null;
    private bool CanDeleteMaintenance() => CanBulk() && WartungIds.Count > 0;
    private bool CanDeleteAlarm() => CanBulk() && AlarmIds.Count > 0;
    private bool CanToggleAlarm() => CanBulk() && AlarmIds.Count > 0;

    private async Task ReloadAfterSave() { await LoadCore(CancellationToken.None); if (!string.IsNullOrEmpty(Status)) Status = "Gespeichert; die Liste konnte nicht aktualisiert werden. Bitte neu laden."; }
    private static Wartung ToCore(WartungsdatensatzAntwort x) => new() { Id=x.Id, Datum=x.Datum, EquipmentId=x.EquipmentId, Taetigkeit=x.Taetigkeit, Kommentar=x.Kommentar, Typ=x.Art switch { Wartungsart.Wartung=>WartungsTyp.Wartung, Wartungsart.Alarm=>WartungsTyp.Alarm, _=>throw new InvalidOperationException() }, AlarmKm=x.AlarmKm, Aktiv=x.Aktiv };
    private static Datenbestand CoreData(StammdatenAntwort meta, WartungsbestandAntwort snapshot)
    {
        var data = new Datenbestand();
        foreach (var a in meta.Aktivitaeten) data.Aktivitaeten.Add(new Aktivitaet { Id=a.Id, Name=a.Name, IstHaupt=a.IstHaupt, Geschwindigkeitseinheit=a.Geschwindigkeitseinheit, ZielKm=a.ZielKm, ZielHm=a.ZielHm, ZielAnzahl=a.ZielAnzahl });
        foreach (var e in meta.Equipments) data.Equipments.Add(new Equipment { Id=e.Id, Name=e.Name, AktivitaetId=e.AktivitaetId, Ausgemustert=e.Ausgemustert, Anschaffungsdatum=e.Anschaffungsdatum, AnfangsdistanzKm=e.AnfangsdistanzKm });
        foreach (var x in snapshot.Eintraege) data.Wartungen.Add(ToCore(x));
        return data;
    }
}
