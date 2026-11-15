using System.Collections.ObjectModel;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

/// <summary>Clientseitiges ViewModel für versionierte Aktivitätenverwaltung.</summary>
public partial class ApiAktivitaetenViewModel : ObservableObject
{
    private readonly IAktivitaetenVerwaltung _verwaltung;
    private readonly Func<string, bool> _bestaetigen;
    private string? _geladeneVersion;

    [ObservableProperty] private AktivitaetAntwort? _auswahl;
    [ObservableProperty] private AktivitaetAntwort? _ziel;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _statusText = "Aktivitäten laden.";
    [ObservableProperty] private bool _laedt;
    [ObservableProperty] private bool _aendert;
    [ObservableProperty] private bool _hatAenderungen;

    /// <summary>Erstellt das ViewModel. Ohne bestätigenden UI-Callback sind destruktive Aktionen abgelehnt.</summary>
    public ApiAktivitaetenViewModel(IAktivitaetenVerwaltung verwaltung, Func<string, bool>? bestaetigen = null)
    {
        _verwaltung = verwaltung ?? throw new ArgumentNullException(nameof(verwaltung));
        _bestaetigen = bestaetigen ?? (_ => false);
        Eintraege = [];
    }

    /// <summary>Aktivitäten in Serverreihenfolge.</summary>
    public ObservableCollection<AktivitaetAntwort> Eintraege { get; }

    /// <summary>Gültige Zusammenführungsziele außer der aktuellen Auswahl.</summary>
    public IReadOnlyList<AktivitaetAntwort> ZielOptionen =>
        Eintraege.Where(a => a.Id != Auswahl?.Id).ToArray();

    partial void OnAuswahlChanged(AktivitaetAntwort? value)
    {
        NameText = value?.Name ?? string.Empty;
        Ziel = null;
        OnPropertyChanged(nameof(ZielOptionen));
        AktualisiereCommands();
    }

    partial void OnZielChanged(AktivitaetAntwort? value) => AktualisiereCommands();
    partial void OnNameTextChanged(string value) => AktualisiereCommands();

    partial void OnLaedtChanged(bool value)
    {
        Aendert = value && _istSchreibvorgang;
        AktualisiereCommands();
    }

    private bool _istSchreibvorgang;

    partial void OnAendertChanged(bool value) => AktualisiereCommands();
    partial void OnHatAenderungenChanged(bool value) => AktualisiereCommands();

    [RelayCommand(CanExecute = nameof(KannLaden))]
    private async Task LadenAsync(CancellationToken ct)
    {
        if (Laedt) return;
        Laedt = true;
        _istSchreibvorgang = false;
        Aendert = false;
        StatusText = "Aktivitäten werden geladen …";
        try
        {
            var antwort = await _verwaltung.Laden(ct);
            SetzeSnapshot(antwort, Auswahl?.Id);
            HatAenderungen = false;
            StatusText = $"{Eintraege.Count} Aktivitäten geladen.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            StatusText = "Laden der Aktivitäten abgebrochen.";
        }
        catch (Exception)
        {
            StatusText = "Aktivitäten konnten nicht geladen werden. Bitte Verbindung prüfen.";
        }
        finally
        {
            _istSchreibvorgang = false;
            Laedt = false;
            Aendert = false;
        }
    }

    [RelayCommand(CanExecute = nameof(KannAnlegen))]
    private Task AnlegenAsync(CancellationToken ct) =>
        SchreibeAsync(() => _verwaltung.Anlegen(new AktivitaetSpeichernAnfrage(_geladeneVersion!, NameText.Trim()), ct),
            "Aktivität angelegt.", ct);

    [RelayCommand(CanExecute = nameof(KannUmbenennen))]
    private Task UmbenennenAsync(CancellationToken ct) =>
        SchreibeAsync(() => _verwaltung.Umbenennen(Auswahl!.Id,
                new AktivitaetSpeichernAnfrage(_geladeneVersion!, NameText.Trim()), ct),
            "Aktivität umbenannt.", ct);

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task LoeschenAsync(CancellationToken ct)
    {
        var aktivitaet = Auswahl;
        if (aktivitaet is null || !KannLoeschen()) return;
        if (!_bestaetigen($"Aktivität „{aktivitaet.Name}“ löschen?")) return;
        await SchreibeAsync(() => _verwaltung.Loeschen(aktivitaet.Id, _geladeneVersion!, ct),
            "Aktivität gelöscht.", ct);
    }

    [RelayCommand(CanExecute = nameof(KannHauptSetzen))]
    private Task HauptSetzenAsync(CancellationToken ct) =>
        SchreibeAsync(() => _verwaltung.SetzeHauptaktivitaet(
                new AktivitaetAuswahlAnfrage(_geladeneVersion!, Auswahl!.Id), ct),
            "Hauptaktivität geändert.", ct);

    [RelayCommand(CanExecute = nameof(KannNachOben))]
    private Task NachObenAsync(CancellationToken ct) => AendereReihenfolgeAsync(-1, ct);

    [RelayCommand(CanExecute = nameof(KannNachUnten))]
    private Task NachUntenAsync(CancellationToken ct) => AendereReihenfolgeAsync(1, ct);

    [RelayCommand(CanExecute = nameof(KannZusammenfuehren))]
    private async Task ZusammenfuehrenAsync(CancellationToken ct)
    {
        var quelle = Auswahl;
        var ziel = Ziel;
        if (quelle is null || ziel is null || !KannZusammenfuehren()) return;
        var meldung = $"„{quelle.Name}“ mit „{ziel.Name}“ zusammenführen? Alle Touren und Equipments werden auf das Ziel übertragen. Saisonziele der Quellaktivität entfallen; die Ziele der Zielaktivität bleiben erhalten.";
        if (!_bestaetigen(meldung)) return;
        await SchreibeAsync(() => _verwaltung.Zusammenfuehren(
                new AktivitaetenZusammenfuehrenAnfrage(_geladeneVersion!, quelle.Id, ziel.Id), ct),
            "Aktivitäten zusammengeführt.", ct, ziel.Id);
    }

    private async Task AendereReihenfolgeAsync(int delta, CancellationToken ct)
    {
        if (Auswahl is null || _geladeneVersion is null || Laedt) return;
        var index = Eintraege.IndexOf(Auswahl);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= Eintraege.Count) return;
        var ids = Eintraege.Select(a => a.Id).ToArray();
        (ids[index], ids[target]) = (ids[target], ids[index]);
        await SchreibeAsync(() => _verwaltung.SetzeReihenfolge(
                new AktivitaetenReihenfolgeAnfrage(_geladeneVersion, ids), ct),
            "Aktivitätenreihenfolge geändert.", ct, Auswahl.Id);
    }

    private async Task SchreibeAsync(Func<Task<AktivitaetenAntwort>> mutation, string erfolg, CancellationToken ct, long? bevorzugteId = null)
    {
        if (Laedt || _geladeneVersion is null) return;
        _istSchreibvorgang = true;
        Laedt = true;
        Aendert = true;
        StatusText = "Änderung wird gespeichert …";
        try
        {
            var antwort = await mutation();
            SetzeSnapshot(antwort, bevorzugteId ?? Auswahl?.Id);
            HatAenderungen = true;
            StatusText = erfolg;
        }
        catch (StammdatenException fehler)
        {
            StatusText = fehler.Art == StammdatenFehlerart.Konflikt
                ? $"{fehler.Message} Bitte neu laden; Eingaben bleiben erhalten."
                : fehler.Message;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            StatusText = "Speichern abgebrochen; der Serverstatus ist möglicherweise unklar. Bitte neu laden und nicht blind erneut ausführen.";
        }
        catch (Exception)
        {
            StatusText = "Speicherstatus unklar. Bitte zuerst neu laden und die Änderung prüfen; nicht erneut absenden.";
        }
        finally
        {
            _istSchreibvorgang = false;
            Laedt = false;
            Aendert = false;
        }
    }

    private void SetzeSnapshot(AktivitaetenAntwort antwort, long? bevorzugteId)
    {
        _geladeneVersion = antwort.Version;
        Eintraege.Clear();
        foreach (var eintrag in antwort.Eintraege) Eintraege.Add(eintrag);
        Auswahl = bevorzugteId is { } id ? Eintraege.FirstOrDefault(a => a.Id == id) : null;
        OnPropertyChanged(nameof(ZielOptionen));
        AktualisiereCommands();
    }

    private bool HatVersion => !string.IsNullOrWhiteSpace(_geladeneVersion);
    private bool KannLaden() => !Laedt;
    private bool KannAnlegen() => !Laedt && HatVersion && IstNameGueltig(null);
    private bool KannUmbenennen() => !Laedt && HatVersion && Auswahl is not null && NameText.Trim() != Auswahl.Name && IstNameGueltig(Auswahl.Id);
    private bool KannLoeschen() => !Laedt && HatVersion && Auswahl is not null && !Auswahl.IstHaupt && Eintraege.Count > 1;
    private bool KannHauptSetzen() => !Laedt && HatVersion && Auswahl is not null && !Auswahl.IstHaupt;
    private bool KannNachOben() => !Laedt && HatVersion && Auswahl is not null && Eintraege.IndexOf(Auswahl) > 0;
    private bool KannNachUnten() => !Laedt && HatVersion && Auswahl is not null && Eintraege.IndexOf(Auswahl) >= 0 && Eintraege.IndexOf(Auswahl) < Eintraege.Count - 1;
    private bool KannZusammenfuehren() => !Laedt && HatVersion && Auswahl is not null && Ziel is not null && Auswahl.Id != Ziel.Id;

    private bool IstNameGueltig(long? ausgenommeneId)
    {
        var name = NameText.Trim();
        return name.Length is > 0 and <= 500 && !name.Any(char.IsControl) &&
            !Eintraege.Any(a => a.Id != ausgenommeneId && string.Equals(a.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    private void AktualisiereCommands()
    {
        LadenCommand.NotifyCanExecuteChanged();
        AnlegenCommand.NotifyCanExecuteChanged();
        UmbenennenCommand.NotifyCanExecuteChanged();
        LoeschenCommand.NotifyCanExecuteChanged();
        HauptSetzenCommand.NotifyCanExecuteChanged();
        NachObenCommand.NotifyCanExecuteChanged();
        NachUntenCommand.NotifyCanExecuteChanged();
        ZusammenfuehrenCommand.NotifyCanExecuteChanged();
    }
}
