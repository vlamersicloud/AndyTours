using System.Collections.ObjectModel;
using System.Globalization;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndyTours.App.ViewModels;

/// <summary>Verwaltet genau eine typisierte Textliste ausschließlich über den Backendport.</summary>
public partial class ApiTextlisteViewModel : ObservableObject, IDisposable
{
    private readonly Textlistenart _art;
    private readonly ITextlistenVerwaltung _verwaltung;
    private readonly Func<string, bool> _bestaetigen;
    private readonly CancellationTokenSource _readLifetime = new();
    private string _version = string.Empty;
    private bool _geladen;
    private bool _schliessend;
    private bool _disposed;

    [ObservableProperty] private string? _auswahl;
    [ObservableProperty] private string _nameText = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _laedt;
    [ObservableProperty] private bool _aendert;
    [ObservableProperty] private bool _hatAenderungen;

    public ApiTextlisteViewModel(Textlistenart art, ITextlistenVerwaltung verwaltung, Func<string, bool>? bestaetigen = null)
    {
        _art = art is Textlistenart.Trainingsbereich or Textlistenart.Taetigkeit
            ? art : throw new ArgumentOutOfRangeException(nameof(art));
        _verwaltung = verwaltung ?? throw new ArgumentNullException(nameof(verwaltung));
        _bestaetigen = bestaetigen ?? (_ => false);
        Eintraege = [];
    }

    /// <summary>Art dieser unveränderlich gebundenen Textliste.</summary>
    public Textlistenart Art => _art;
    /// <summary>Servergeladene Einträge in aktueller Reihenfolge.</summary>
    public ObservableCollection<string> Eintraege { get; }

    partial void OnAuswahlChanged(string? value)
    {
        NameText = value ?? string.Empty;
        NotifyCommands();
    }
    partial void OnNameTextChanged(string value) => NotifyCommands();
    partial void OnLaedtChanged(bool value) => NotifyCommands();
    partial void OnAendertChanged(bool value) => NotifyCommands();

    private void NotifyCommands()
    {
        LadenCommand.NotifyCanExecuteChanged(); AnlegenCommand.NotifyCanExecuteChanged();
        UmbenennenCommand.NotifyCanExecuteChanged(); LoeschenCommand.NotifyCanExecuteChanged();
        NachObenCommand.NotifyCanExecuteChanged(); NachUntenCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanLoad))]
    public async Task LadenAsync(CancellationToken ct = default)
    {
        if (_schliessend || Laedt || Aendert) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _readLifetime.Token);
        _geladen = false;
        NotifyCommands();
        Laedt = true;
        try
        {
            var response = await _verwaltung.Laden(_art, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (_schliessend) return;
            SetSnapshot(response, null);
            Status = string.Empty;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (StammdatenException e) { if (!_schliessend) Status = e.Message; }
        catch (Exception) { if (!_schliessend) Status = "Textliste konnte nicht geladen werden."; }
        finally { if (!_schliessend) Laedt = false; }
    }

    private bool CanLoad() => !_schliessend && !Laedt && !Aendert;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private Task AnlegenAsync()
    {
        var name = BereinigterName();
        var version = _version;
        return Mutate(token => _verwaltung.Anlegen(_art, new(version, name), token), name);
    }
    private bool CanAdd() => CanIdle() && TryName(out var name) && !Eintraege.Any(x => EqualsName(x, name));

    [RelayCommand(CanExecute = nameof(CanRename))]
    private async Task UmbenennenAsync()
    {
        if (!CanRename() || Auswahl is not string oldName) return;
        var newName = BereinigterName();
        var version = _version;
        var token = _readLifetime.Token;
        var writeStarted = false;
        Aendert = true;
        try
        {
            var usage = await _verwaltung.NutzungLesen(_art, version, oldName, token);
            token.ThrowIfCancellationRequested();
            if (_schliessend || !string.Equals(_version, version, StringComparison.Ordinal) ||
                !string.Equals(Auswahl, oldName, StringComparison.Ordinal) ||
                !string.Equals(BereinigterName(), newName, StringComparison.Ordinal)) return;
            if (usage.Art != _art || usage.ListenVersion != version || usage.Name != oldName ||
                usage.VerwendungsVersion.Length != 64 || !usage.VerwendungsVersion.All(Uri.IsHexDigit) ||
                usage.Touren < 0 || usage.Wartungen < 0 || usage.Alarme < 0 ||
                (_art == Textlistenart.Trainingsbereich && (usage.Wartungen != 0 || usage.Alarme != 0)) ||
                (_art == Textlistenart.Taetigkeit && usage.Touren != 0))
                throw new InvalidOperationException("Die Nutzungsvorschau ist ungültig.");
            var total = (long)usage.Touren + usage.Wartungen + usage.Alarme;
            if (total > 0)
            {
                var description = _art == Textlistenart.Trainingsbereich
                    ? $"{usage.Touren.ToString("N0", CultureInfo.GetCultureInfo("de-DE"))} {(usage.Touren == 1 ? "Tour" : "Touren")}"
                    : $"{usage.Wartungen.ToString("N0", CultureInfo.GetCultureInfo("de-DE"))} {(usage.Wartungen == 1 ? "Wartung" : "Wartungen")} und {usage.Alarme.ToString("N0", CultureInfo.GetCultureInfo("de-DE"))} {(usage.Alarme == 1 ? "Alarm" : "Alarme")}";
                if (!_bestaetigen($"In {description} ‚{oldName}‘ durch ‚{newName}‘ ersetzen?")) return;
            }
            token.ThrowIfCancellationRequested();
            if (_schliessend || !string.Equals(_version, version, StringComparison.Ordinal) ||
                !string.Equals(Auswahl, oldName, StringComparison.Ordinal) ||
                !string.Equals(BereinigterName(), newName, StringComparison.Ordinal)) return;
            writeStarted = true;
            var response = await _verwaltung.Umbenennen(_art,
                new(version, oldName, newName, usage.VerwendungsVersion), token);
            token.ThrowIfCancellationRequested();
            if (_schliessend) return;
            SetSnapshot(response, newName);
            HatAenderungen = true;
            Status = string.Empty;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (!_schliessend && writeStarted)
            {
                HatAenderungen = true;
                SperreBisNeuladen();
                Status = "Speicherstatus unklar. Bitte neu laden und prüfen; nicht blind erneut speichern.";
            }
        }
        catch (StammdatenException e)
        {
            if (!_schliessend)
            {
                if (e.Art is StammdatenFehlerart.Konflikt or StammdatenFehlerart.NichtGefunden) SperreBisNeuladen();
                Status = e.Art == StammdatenFehlerart.Konflikt ? e.Message + " Bitte neu laden; die Eingabe bleibt erhalten." : e.Message;
            }
        }
        catch (Exception)
        {
            if (!_schliessend)
            {
                if (writeStarted) { HatAenderungen = true; SperreBisNeuladen(); }
                Status = writeStarted ? "Speicherstatus unklar. Bitte die Textliste neu laden und prüfen; nicht blind erneut speichern." : "Nutzungsvorschau konnte nicht geprüft werden; es wurde nichts geändert.";
            }
        }
        finally { if (!_schliessend) Aendert = false; }
    }
    private bool CanRename() => CanIdle() && Auswahl is not null && TryName(out var name) &&
        !string.Equals(Auswahl, name, StringComparison.Ordinal) &&
        (!Eintraege.Any(x => EqualsName(x, name)) || EqualsName(Auswahl, name));

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task LoeschenAsync()
    {
        if (!CanDelete() || Auswahl is not string name) return;
        var version = _version;
        var token = _readLifetime.Token;
        var writeStarted = false;
        Aendert = true;
        try
        {
            var usage = await _verwaltung.NutzungLesen(_art, version, name, token);
            token.ThrowIfCancellationRequested();
            if (_schliessend || _version != version || Auswahl != name) return;
            if (usage.Art != _art || usage.ListenVersion != version || usage.Name != name ||
                usage.VerwendungsVersion.Length != 64 || !usage.VerwendungsVersion.All(Uri.IsHexDigit) ||
                usage.Touren < 0 || usage.Wartungen < 0 || usage.Alarme < 0 ||
                (_art == Textlistenart.Trainingsbereich && (usage.Wartungen != 0 || usage.Alarme != 0)) ||
                (_art == Textlistenart.Taetigkeit && usage.Touren != 0))
                throw new InvalidOperationException("Die Nutzungsvorschau ist ungültig.");
            if ((long)usage.Touren + usage.Wartungen + usage.Alarme > 0)
            {
                Status = _art == Textlistenart.Trainingsbereich
                    ? $"Löschen nicht möglich: Der Eintrag wird in {usage.Touren.ToString("N0", CultureInfo.GetCultureInfo("de-DE"))} {(usage.Touren == 1 ? "Tour" : "Touren")} verwendet."
                    : $"Löschen nicht möglich: {usage.Wartungen.ToString("N0", CultureInfo.GetCultureInfo("de-DE"))} {(usage.Wartungen == 1 ? "Wartung" : "Wartungen")} und {usage.Alarme.ToString("N0", CultureInfo.GetCultureInfo("de-DE"))} {(usage.Alarme == 1 ? "Alarm" : "Alarme")} verwenden den Eintrag.";
                return;
            }
            if (!_bestaetigen($"„{name}“ aus der Liste löschen?")) return;
            token.ThrowIfCancellationRequested();
            if (_schliessend || _version != version || Auswahl != name) return;
            writeStarted = true;
            var response = await _verwaltung.Loeschen(_art, new(version, name), token);
            if (_schliessend) return;
            SetSnapshot(response, null);
            HatAenderungen = true;
            Status = string.Empty;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (!_schliessend && writeStarted)
            {
                HatAenderungen = true;
                SperreBisNeuladen();
                Status = "Speicherstatus unklar. Bitte neu laden und prüfen; nicht blind erneut speichern.";
            }
        }
        catch (StammdatenException e)
        {
            if (!_schliessend)
            {
                if (e.Art is StammdatenFehlerart.Konflikt or StammdatenFehlerart.NichtGefunden) SperreBisNeuladen();
                Status = e.Message;
            }
        }
        catch (Exception)
        {
            if (!_schliessend)
            {
                if (writeStarted) { HatAenderungen = true; SperreBisNeuladen(); }
                Status = writeStarted ? "Speicherstatus unklar. Bitte die Textliste neu laden und prüfen; nicht blind erneut speichern." : "Nutzungsvorschau konnte nicht geprüft werden; es wurde nichts geändert.";
            }
        }
        finally { if (!_schliessend) Aendert = false; }
    }
    private bool CanDelete() => CanIdle() && Auswahl is not null;

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private Task NachObenAsync() => Move(-1);
    private bool CanMoveUp() => CanIdle() && IndexAuswahl() > 0;

    private bool CanMoveDown() => CanIdle() && IndexAuswahl() >= 0 && IndexAuswahl() < Eintraege.Count - 1;
    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private Task NachUntenAsync() => Move(1);

    private async Task Move(int offset)
    {
        var from = IndexAuswahl();
        if (from < 0) return;
        var reordered = Eintraege.ToList();
        var to = from + offset;
        if (to < 0 || to >= reordered.Count) return;
        (reordered[from], reordered[to]) = (reordered[to], reordered[from]);
        var selected = Auswahl;
        var version = _version;
        await Mutate(token => _verwaltung.SetzeReihenfolge(_art, new(version, reordered), token), selected);
    }

    private bool CanIdle() => !_schliessend && _geladen && !Laedt && !Aendert;
    private int IndexAuswahl() => Auswahl is null ? -1 : Eintraege.IndexOf(Auswahl);

    private async Task Mutate(Func<CancellationToken, Task<TextlisteAntwort>> action, string? reselection = null)
    {
        if (!CanIdle()) return;
        var token = _readLifetime.Token;
        var version = _version;
        var writeStarted = false;
        Aendert = true;
        try
        {
            token.ThrowIfCancellationRequested();
            if (_schliessend || _version != version) return;
            writeStarted = true;
            var response = await action(token);
            token.ThrowIfCancellationRequested();
            if (_schliessend) return;
            SetSnapshot(response, reselection);
            HatAenderungen = true;
            Status = string.Empty;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (!_schliessend && writeStarted)
            {
                HatAenderungen = true;
                SperreBisNeuladen();
                Status = "Speicherstatus unklar. Bitte neu laden und prüfen; nicht blind erneut speichern.";
            }
        }
        catch (StammdatenException e)
        {
            if (!_schliessend)
            {
                if (e.Art is StammdatenFehlerart.Konflikt or StammdatenFehlerart.NichtGefunden) SperreBisNeuladen();
                Status = e.Art == StammdatenFehlerart.Konflikt ? e.Message + " Bitte neu laden; die Eingabe bleibt erhalten." : e.Message;
            }
        }
        catch (Exception)
        {
            if (!_schliessend)
            {
                if (writeStarted) { HatAenderungen = true; SperreBisNeuladen(); }
                Status = "Speicherstatus unklar. Bitte die Textliste neu laden und prüfen; nicht blind erneut speichern.";
            }
        }
        finally { if (!_schliessend) Aendert = false; }
    }

    private void SetSnapshot(TextlisteAntwort response, string? reselection)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.Art != _art || response.Eintraege is null ||
            response.Version is not { Length: 64 } || !response.Version.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Textlistenantwort ist ungültig.");
        var entries = response.Eintraege.ToArray();
        if (entries.Any(item => string.IsNullOrWhiteSpace(item) || item != item.Trim() || item.Length > 500 || item.Any(char.IsControl)) ||
            entries.Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length)
            throw new InvalidOperationException("Textlistenantwort ist ungültig.");

        _version = response.Version;
        Auswahl = null;
        Eintraege.Clear();
        foreach (var item in entries) Eintraege.Add(item);
        Auswahl = reselection is null ? null : Eintraege.FirstOrDefault(x => EqualsName(x, reselection));
        NameText = Auswahl ?? string.Empty;
        _geladen = true;
        NotifyCommands();
    }

    private void SperreBisNeuladen()
    {
        _geladen = false;
        NotifyCommands();
    }

    private bool TryName(out string name)
    {
        name = NameText.Trim();
        return name.Length is >= 1 and <= 500 && !name.Any(char.IsControl);
    }
    private string BereinigterName() => NameText.Trim();
    private static bool EqualsName(string left, string right) => string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    public void AbbrechenLesen()
    {
        if (_schliessend) return;
        _schliessend = true;
        _readLifetime.Cancel();
        NotifyCommands();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        AbbrechenLesen();
        _readLifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
