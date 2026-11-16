using System.Collections.ObjectModel;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

/// <summary>Clientseitige versionierte Equipmentverwaltung ohne lokalen Datenbankzugriff.</summary>
public partial class ApiEquipmentsViewModel : ObservableObject
{
    private readonly IEquipmentsVerwaltung _verwaltung;
    private readonly IStammdatenLeser _stammdaten;
    private readonly Func<string, bool> _bestaetigen;
    private string? _version;
    private Equipment? _basis;

    [ObservableProperty] private EquipmentAntwort? _auswahl;
    [ObservableProperty] private string _statusText = "Equipment laden.";
    [ObservableProperty] private bool _laedt;
    [ObservableProperty] private bool _aendert;
    [ObservableProperty] private bool _editorOffen;
    [ObservableProperty] private bool _hatAenderungen;

    public ApiEquipmentsViewModel(IEquipmentsVerwaltung verwaltung, IStammdatenLeser stammdaten, Func<string, bool>? bestaetigen = null)
    {
        _verwaltung = verwaltung ?? throw new ArgumentNullException(nameof(verwaltung));
        _stammdaten = stammdaten ?? throw new ArgumentNullException(nameof(stammdaten));
        _bestaetigen = bestaetigen ?? (_ => false);
        Eintraege = [];
    }

    public ObservableCollection<EquipmentAntwort> Eintraege { get; }
    partial void OnAuswahlChanged(EquipmentAntwort? value) => AktualisiereCommands();
    partial void OnLaedtChanged(bool value) { if (!value) Aendert = false; AktualisiereCommands(); }
    partial void OnAendertChanged(bool value) => AktualisiereCommands();
    partial void OnEditorOffenChanged(bool value) => AktualisiereCommands();

    [RelayCommand(CanExecute = nameof(KannLaden))]
    public async Task LadenAsync(CancellationToken ct = default)
    {
        if (Laedt || EditorOffen) return;
        Laedt = true; StatusText = "Equipment wird geladen …";
        try
        {
            var oldId = Auswahl?.Id;
            var response = await _verwaltung.Laden(ct);
            SetzeSnapshot(response, oldId);
            StatusText = $"{Eintraege.Count} Einträge geladen.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { StatusText = "Laden abgebrochen."; }
        catch (Exception) { StatusText = "Equipment konnte nicht geladen werden. Bitte Verbindung prüfen."; }
        finally { Laedt = false; }
    }

    [RelayCommand(CanExecute = nameof(KannNeu))]
    private Task NeuAsync(CancellationToken ct = default) => OeffneEditorAsync(null, ct);

    [RelayCommand(CanExecute = nameof(KannBearbeiten))]
    private Task BearbeitenAsync(CancellationToken ct = default) => OeffneEditorAsync(Auswahl, ct);

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task LoeschenAsync(CancellationToken ct = default)
    {
        var selected = Auswahl;
        if (selected is null || !KannLoeschen() || !_bestaetigen($"Equipment „{selected.Name}“ löschen? Verwendete Einträge oder Wartungen verhindern das Löschen.")) return;
        Laedt = true; Aendert = true; StatusText = "Equipment wird gelöscht …";
        try
        {
            var response = await _verwaltung.Loeschen(selected.Id, _version!, ct);
            SetzeSnapshot(response, null); HatAenderungen = true; StatusText = "Equipment gelöscht.";
        }
        catch (StammdatenException ex) { StatusText = ex.Art == StammdatenFehlerart.Konflikt ? ex.Message + " Bitte neu laden." : ex.Message; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { MarkiereUnklar("Löschen abgebrochen; Status möglicherweise unklar. Erst neu laden und prüfen."); }
        catch (Exception) { MarkiereUnklar("Löschstatus unklar. Bitte neu laden und prüfen; nicht blind erneut löschen."); }
        finally { Laedt = false; Aendert = false; }
    }

    private async Task OeffneEditorAsync(EquipmentAntwort? selected, CancellationToken ct)
    {
        if (Laedt || EditorOffen) return;
        Laedt = true; StatusText = "Aktuelle Daten werden geladen …";
        try
        {
            var family = await _verwaltung.Laden(ct);
            var metadataResponse = await _stammdaten.Laden(ct);
            var metadata = ApiFormularMapper.AlsStammdaten(metadataResponse);
            SetzeSnapshot(family, selected?.Id);
            var found = selected is null ? null : Eintraege.FirstOrDefault(e => e.Id == selected.Id);
            _basis = found is null ? new Equipment() : ToEquipment(found);
            var input = EquipmentEingaben.AlsEingaben(_basis);
            if (found is null && input.AktivitaetId is null)
                input = input with { AktivitaetId = metadata.Aktivitaeten.FirstOrDefault(a => a.IstHaupt)?.Id };
            var vm = new EquipmentBearbeitenViewModel(input,
                metadata.Aktivitaeten.Select(a => new Auswahleintrag(a.Id, a.Name)), metadata.Kategorien.Select(k => k.Name),
                found is null ? "Neues Equipment" : "Equipment bearbeiten",
                values => EquipmentEingaben.AlsEquipment(values, _basis, metadata).Hinweis,
                values => SpeichernAsync(values, _basis, metadata, family.Version, ct));
            vm.Gespeichert += (_, _) => { EditorOffen = false; StatusText = "Equipment gespeichert."; };
            EditorOffen = true;
            StatusText = "Editor geöffnet.";
            EditorAngefordert?.Invoke(this, new EquipmentEditorEventArgs(vm));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { StatusText = "Editoröffnung abgebrochen."; }
        catch (Exception) { StatusText = "Aktuelle Stammdaten konnten nicht geladen werden. Bitte Verbindung prüfen."; }
        finally { Laedt = false; }
    }

    public event EventHandler<EquipmentEditorEventArgs>? EditorAngefordert;

    private async Task<bool> SpeichernAsync(Equipmenteingaben input, Equipment basis, Datenbestand metadata, string version, CancellationToken ct)
    {
        var (equipment, error) = EquipmentEingaben.AlsEquipment(input, basis, metadata);
        if (equipment is null) { StatusText = error ?? "Eingaben ungültig."; return false; }
        var original = basis;
        if (original.Id > 0 && input.Radkennungen.SequenceEqual(GeraetezuordnungText.Lesen(original.Geraetezuordnung)))
            equipment.Geraetezuordnung = original.Geraetezuordnung;
        Laedt = true; Aendert = true; StatusText = "Equipment wird gespeichert …";
        try
        {
            var values = new EquipmentWerte(equipment.Name, equipment.AktivitaetId, equipment.Geraetezuordnung,
                equipment.StandardKategorie, equipment.IstStandard, equipment.Ausgemustert, equipment.Anschaffungsdatum, equipment.AnfangsdistanzKm);
            var request = new EquipmentSpeichernAnfrage(version, values);
            var response = equipment.Id == 0 ? await _verwaltung.Anlegen(request, ct) : await _verwaltung.Aendern(equipment.Id, request, ct);
            SetzeSnapshot(response, equipment.Id);
            HatAenderungen = true;
            return true;
        }
        catch (StammdatenException ex)
        {
            StatusText = ex.Art == StammdatenFehlerart.Konflikt ? ex.Message + " Eingaben bleiben erhalten; bitte neu laden." : ex.Message;
            return false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            MarkiereUnklar("Speichern abgebrochen; Speicherstatus unklar. Bitte neu laden und prüfen, nicht blind erneut speichern.");
            return false;
        }
        catch (Exception)
        {
            MarkiereUnklar("Speicherstatus unklar. Bitte neu laden und prüfen, nicht blind erneut speichern.");
            return false;
        }
        finally { Laedt = false; Aendert = false; }
    }

    private void SetzeSnapshot(EquipmentsAntwort response, long? preferredId)
    {
        _version = response.Version;
        Auswahl = null; Eintraege.Clear();
        foreach (var item in response.Eintraege) Eintraege.Add(item);
        Auswahl = preferredId is { } id ? Eintraege.FirstOrDefault(e => e.Id == id) : null;
    }

    private void MarkiereUnklar(string message) { HatAenderungen = true; StatusText = message; }
    private static Equipment ToEquipment(EquipmentAntwort e) => new() { Id = e.Id, Name = e.Name, AktivitaetId = e.AktivitaetId,
        Geraetezuordnung = e.Geraetezuordnung, StandardKategorie = e.StandardKategorie, IstStandard = e.IstStandard,
        Ausgemustert = e.Ausgemustert, Anschaffungsdatum = e.Anschaffungsdatum, AnfangsdistanzKm = e.AnfangsdistanzKm };
    private bool KannLaden() => !Laedt && !EditorOffen;
    private bool KannNeu() => !Laedt && !EditorOffen && _version is not null;
    private bool KannBearbeiten() => KannNeu() && Auswahl is not null;
    private bool KannLoeschen() => KannBearbeiten();
    private void AktualisiereCommands() { LadenCommand.NotifyCanExecuteChanged(); NeuCommand.NotifyCanExecuteChanged(); BearbeitenCommand.NotifyCanExecuteChanged(); LoeschenCommand.NotifyCanExecuteChanged(); }
}

public sealed class EquipmentEditorEventArgs(EquipmentBearbeitenViewModel viewModel) : EventArgs
{
    public EquipmentBearbeitenViewModel ViewModel { get; } = viewModel;
}
