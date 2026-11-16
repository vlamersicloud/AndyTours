using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AndyTours.Core.Modell;

namespace AndyTours.App.ViewModels;

public partial class GarminImportViewModel : ObservableObject, IDisposable
{
    public const string Keines = "(keines)";
    private readonly Func<IReadOnlyList<Importzeile>, Task<bool>>? _speichern;
    private bool _disposed;
    public GarminImportViewModel(IReadOnlyList<Importzeile> zeilen, IEnumerable<Auswahleintrag> equipments) : this(zeilen, equipments, null) { }
    public GarminImportViewModel(IReadOnlyList<Importzeile> zeilen, IEnumerable<Auswahleintrag> equipments, Func<IReadOnlyList<Importzeile>, Task<bool>>? speichern)
    {
        Zeilen = zeilen ?? throw new ArgumentNullException(nameof(zeilen));
        Equipments = [new Auswahleintrag(null, Keines), .. equipments];
        MehrereGeraete = zeilen.Select(z => z.Geraet).Distinct().Count() > 1;
        _speichern = speichern;
        foreach (var zeile in zeilen) zeile.PropertyChanged += ZeileGeaendert;
    }
    public event EventHandler? Bestaetigt;
    public IReadOnlyList<Importzeile> Zeilen { get; }
    public IReadOnlyList<Auswahleintrag> Equipments { get; }
    public bool MehrereGeraete { get; }
    public IReadOnlyList<Importzeile> Ergebnis { get; private set; } = [];
    [ObservableProperty] private bool speichert;
    [ObservableProperty] private string? hinweis;
    [ObservableProperty] private string _zusatzhinweis = string.Empty;
    public string ErklaerungText => _speichern is null ? "Diese Touren sind noch nicht in AndyTours. Angehakte Touren werden mit dem gewählten Equipment importiert." : "Nicht markierte Zeilen werden im Garmin-Auftrag übersprungen und nicht gelöscht; Originaldateien bleiben erhalten.";
    public string BestaetigenText => _speichern is null ? "Importieren" : "Auswahl bestätigen";
    [RelayCommand(CanExecute = nameof(KannImportieren))]
    private async Task ImportierenAsync()
    {
        if (_disposed || Speichert || (_speichern is null && !Zeilen.Any(z => z.Importieren)) || Zeilen.Count == 0) return;
        Speichert = true;
        try
        {
            var all = Zeilen.ToArray();
            Ergebnis = all.Where(z => z.Importieren).ToList();
            if (_speichern is not null && !await _speichern(all))
            {
                if (string.IsNullOrWhiteSpace(Hinweis)) Hinweis = "Bestätigung nicht abgeschlossen; Eingaben bleiben erhalten. Bitte prüfen oder aktualisieren.";
                return;
            }
            Bestaetigt?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            if (string.IsNullOrWhiteSpace(Hinweis)) Hinweis = "Ergebnis unklar; der Auftrag kann bereits verarbeitet sein. Bitte Status aktualisieren, nicht blind erneut bestätigen.";
        }
        finally { Speichert = false; }
    }
    private bool KannImportieren() => !_disposed && !Speichert && Zeilen.Count > 0 && (_speichern is not null || Zeilen.Any(z => z.Importieren));
    private void ZeileGeaendert(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(Importzeile.Importieren)) ImportierenCommand.NotifyCanExecuteChanged(); }
    partial void OnSpeichertChanged(bool value) => ImportierenCommand.NotifyCanExecuteChanged();
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var zeile in Zeilen) zeile.PropertyChanged -= ZeileGeaendert;
        GC.SuppressFinalize(this);
    }
}
