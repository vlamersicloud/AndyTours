using System.Collections.ObjectModel;
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

    partial void OnAuswahlChanged(string? value) => NotifyCommands();
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
        catch (StammdatenException e) { Status = e.Message; }
        catch (Exception) { Status = "Textliste konnte nicht geladen werden."; }
        finally { Laedt = false; }
    }

    private bool CanLoad() => !_schliessend && !Laedt && !Aendert;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private Task AnlegenAsync() => Mutate(async () => await _verwaltung.Anlegen(_art, new(_version, BereinigterName())));
    private bool CanAdd() => CanIdle() && TryName(out var name) && !Eintraege.Any(x => EqualsName(x, name));

    [RelayCommand(CanExecute = nameof(CanRename))]
    private Task UmbenennenAsync() => Mutate(async () => await _verwaltung.Umbenennen(_art, new(_version, Auswahl!, BereinigterName())));
    private bool CanRename() => CanIdle() && Auswahl is not null && TryName(out var name) &&
        (!Eintraege.Any(x => EqualsName(x, name)) || EqualsName(Auswahl, name));

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task LoeschenAsync()
    {
        if (Auswahl is null || !_bestaetigen($"„{Auswahl}“ aus der Liste löschen?")) return;
        await Mutate(async () => await _verwaltung.Loeschen(_art, new(_version, Auswahl)));
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
        (reordered[from], reordered[to]) = (reordered[to], reordered[from]);
        var selected = Auswahl;
        await Mutate(async () => await _verwaltung.SetzeReihenfolge(_art, new(_version, reordered)), selected);
    }

    private bool CanIdle() => !_schliessend && _geladen && !Laedt && !Aendert;
    private int IndexAuswahl() => Auswahl is null ? -1 : Eintraege.IndexOf(Auswahl);

    private async Task Mutate(Func<Task<TextlisteAntwort>> action, string? reselection = null)
    {
        if (!CanIdle()) return;
        Aendert = true;
        try
        {
            var response = await action();
            SetSnapshot(response, reselection);
            HatAenderungen = true;
            Status = string.Empty;
        }
        catch (StammdatenException e)
        {
            Status = e.Art == StammdatenFehlerart.Konflikt ? e.Message + " Bitte neu laden; die Eingabe bleibt erhalten." : e.Message;
        }
        catch (Exception)
        {
            HatAenderungen = true;
            Status = "Speicherstatus unklar. Bitte die Textliste neu laden und prüfen; nicht blind erneut speichern.";
        }
        finally { Aendert = false; }
    }

    private void SetSnapshot(TextlisteAntwort response, string? reselection)
    {
        _version = response.Version;
        _geladen = true;
        Eintraege.Clear();
        foreach (var item in response.Eintraege) Eintraege.Add(item);
        Auswahl = reselection is null ? null : Eintraege.FirstOrDefault(x => EqualsName(x, reselection));
        NameText = string.Empty;
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
