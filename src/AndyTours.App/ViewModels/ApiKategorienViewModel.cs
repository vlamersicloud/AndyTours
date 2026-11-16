using System.Collections.ObjectModel;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

/// <summary>Clientseitiges ViewModel für versionierte Kategorienverwaltung.</summary>
public partial class ApiKategorienViewModel : ObservableObject
{
    private readonly IKategorienVerwaltung _verwaltung;
    private readonly Func<string, bool> _bestaetigen;
    private string? _geladeneVersion;
    private bool _istSchreibvorgang;

    [ObservableProperty] private KategorieAntwort? _auswahl;
    [ObservableProperty] private KategorieAntwort? _ziel;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _statusText = "Kategorien laden.";
    [ObservableProperty] private bool _laedt;
    [ObservableProperty] private bool _aendert;
    [ObservableProperty] private bool _hatAenderungen;

    /// <summary>Erstellt das ViewModel; ohne Bestätigungs-Callback sind Löschungen und Zusammenführungen abgelehnt.</summary>
    public ApiKategorienViewModel(IKategorienVerwaltung verwaltung, Func<string, bool>? bestaetigen = null)
    {
        _verwaltung = verwaltung ?? throw new ArgumentNullException(nameof(verwaltung));
        _bestaetigen = bestaetigen ?? (_ => false);
        Eintraege = [];
    }

    /// <summary>Kategorien aus dem letzten bestätigten Serversnapshot.</summary>
    public ObservableCollection<KategorieAntwort> Eintraege { get; }

    /// <summary>Zusammenführungsziele ohne die aktuell ausgewählte Kategorie.</summary>
    public IReadOnlyList<KategorieAntwort> ZielOptionen =>
        Eintraege.Where(k => k.Id != Auswahl?.Id).ToArray();

    partial void OnAuswahlChanged(KategorieAntwort? value)
    {
        NameText = value?.Name ?? string.Empty;
        Ziel = null;
        OnPropertyChanged(nameof(ZielOptionen));
        AktualisiereCommands();
    }

    partial void OnZielChanged(KategorieAntwort? value) => AktualisiereCommands();
    partial void OnNameTextChanged(string value) => AktualisiereCommands();

    partial void OnLaedtChanged(bool value)
    {
        Aendert = value && _istSchreibvorgang;
        AktualisiereCommands();
    }

    partial void OnAendertChanged(bool value) => AktualisiereCommands();

    [RelayCommand(CanExecute = nameof(KannLaden))]
    public async Task LadenAsync(CancellationToken ct = default)
    {
        if (Laedt) return;
        Laedt = true;
        _istSchreibvorgang = false;
        Aendert = false;
        StatusText = "Kategorien werden geladen …";
        try
        {
            var antwort = await _verwaltung.Laden(ct);
            SetzeSnapshot(antwort, Auswahl?.Id);
            StatusText = $"{Eintraege.Count} Kategorien geladen.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            StatusText = "Laden der Kategorien abgebrochen.";
        }
        catch (Exception)
        {
            StatusText = "Kategorien konnten nicht geladen werden. Bitte Verbindung prüfen.";
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
        SchreibeAsync(() => _verwaltung.Anlegen(
                new KategorieSpeichernAnfrage(_geladeneVersion!, NameText.Trim()), ct),
            "Kategorie angelegt.", ct);

    [RelayCommand(CanExecute = nameof(KannUmbenennen))]
    private Task UmbenennenAsync(CancellationToken ct) =>
        SchreibeAsync(() => _verwaltung.Umbenennen(Auswahl!.Id,
                new KategorieSpeichernAnfrage(_geladeneVersion!, NameText.Trim()), ct),
            "Kategorie umbenannt.", ct);

    [RelayCommand(CanExecute = nameof(KannLoeschen))]
    private async Task LoeschenAsync(CancellationToken ct)
    {
        var auswahl = Auswahl;
        if (auswahl is null || !KannLoeschen()) return;
        if (!_bestaetigen($"Kategorie „{auswahl.Name}“ löschen? Verwendete Kategorien können nicht gelöscht werden.")) return;
        await SchreibeAsync(() => _verwaltung.Loeschen(auswahl.Id, _geladeneVersion!, ct),
            "Kategorie gelöscht.", ct);
    }

    [RelayCommand(CanExecute = nameof(KannStandardUmschalten))]
    private Task StandardUmschaltenAsync(CancellationToken ct) =>
        SchreibeAsync(() => _verwaltung.SetzeStandard(new KategorieStandardAnfrage(
                _geladeneVersion!, Auswahl!.Id, !Auswahl.IstStandard), ct),
            Auswahl!.IstStandard ? "Standardkennzeichen entfernt." : "Kategorie als Standard markiert.", ct);

    [RelayCommand(CanExecute = nameof(KannZusammenfuehren))]
    private async Task ZusammenfuehrenAsync(CancellationToken ct)
    {
        var quelle = Auswahl;
        var ziel = Ziel;
        if (quelle is null || ziel is null || !KannZusammenfuehren()) return;
        var meldung = $"„{quelle.Name}“ mit „{ziel.Name}“ zusammenführen? Alle Tourzuordnungen und Equipment-Standardkategorien werden auf das Ziel umgestellt; die Quellkategorie wird entfernt.";
        if (!_bestaetigen(meldung)) return;
        await SchreibeAsync(() => _verwaltung.Zusammenfuehren(
                new KategorienZusammenfuehrenAnfrage(_geladeneVersion!, quelle.Id, ziel.Id), ct),
            "Kategorien zusammengeführt.", ct, ziel.Id);
    }

    private async Task SchreibeAsync(Func<Task<KategorienAntwort>> mutation, string erfolg,
        CancellationToken ct, long? bevorzugteId = null)
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
            MarkiereUnklarenSchreibstatus(
                "Speichern abgebrochen; der Serverstatus ist möglicherweise unklar. Bitte erst neu prüfen und nicht blind erneut senden.");
        }
        catch (Exception)
        {
            MarkiereUnklarenSchreibstatus(
                "Speicherstatus unklar. Bitte zuerst neu laden und die Änderung prüfen; nicht erneut absenden.");
        }
        finally
        {
            _istSchreibvorgang = false;
            Laedt = false;
            Aendert = false;
        }
    }

    private void MarkiereUnklarenSchreibstatus(string meldung)
    {
        HatAenderungen = true;
        StatusText = meldung;
    }

    private void SetzeSnapshot(KategorienAntwort antwort, long? bevorzugteId)
    {
        _geladeneVersion = antwort.Version;
        Auswahl = null;
        Ziel = null;
        Eintraege.Clear();
        foreach (var kategorie in antwort.Eintraege) Eintraege.Add(kategorie);
        Auswahl = bevorzugteId is { } id ? Eintraege.FirstOrDefault(k => k.Id == id) : null;
        NameText = Auswahl?.Name ?? string.Empty;
        OnPropertyChanged(nameof(ZielOptionen));
        AktualisiereCommands();
    }

    private bool HatVersion => !string.IsNullOrWhiteSpace(_geladeneVersion);
    private bool KannLaden() => !Laedt;
    private bool KannAnlegen() => !Laedt && HatVersion && IstNameGueltig(null);
    private bool KannUmbenennen() => !Laedt && HatVersion && Auswahl is not null &&
        NameText.Trim() != Auswahl.Name && IstNameGueltig(Auswahl.Id);
    private bool KannLoeschen() => !Laedt && HatVersion && Auswahl is not null;
    private bool KannStandardUmschalten() => !Laedt && HatVersion && Auswahl is not null;
    private bool KannZusammenfuehren() => !Laedt && HatVersion && Auswahl is not null &&
        Ziel is not null && Auswahl.Id != Ziel.Id;

    private bool IstNameGueltig(long? ausgenommeneId)
    {
        var name = NameText.Trim();
        return name.Length is > 0 and <= 500 && !name.Any(char.IsControl) &&
            !Eintraege.Any(k => k.Id != ausgenommeneId &&
                string.Equals(k.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    private void AktualisiereCommands()
    {
        LadenCommand.NotifyCanExecuteChanged();
        AnlegenCommand.NotifyCanExecuteChanged();
        UmbenennenCommand.NotifyCanExecuteChanged();
        LoeschenCommand.NotifyCanExecuteChanged();
        StandardUmschaltenCommand.NotifyCanExecuteChanged();
        ZusammenfuehrenCommand.NotifyCanExecuteChanged();
    }
}
