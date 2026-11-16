using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AndyTours.Daten.Datenordner;

namespace AndyTours.App.ViewModels;

/// <summary>
/// ViewModel des Fortschrittsfensters: bereitet einen <see cref="KopierFortschritt"/> für die Anzeige auf.
/// </summary>
/// <remarks>
/// Angezeigt, während beim Wechsel des Datenordners der Datenbestand kopiert wird.
/// </remarks>
public partial class FortschrittsViewModel : ObservableObject
{
    /// <summary>Ein Megabyte in Byte (1024 × 1024), für die Mengenangabe.</summary>
    private const double BytesJeMegabyte = 1024 * 1024;

    /// <summary>
    /// Erzeugt das ViewModel.
    /// </summary>
    /// <param name="titel">Fenstertitel, z. B. „Daten werden kopiert …“.</param>
    public FortschrittsViewModel(string titel, Action? abbrechen = null)
    {
        Titel = titel;
        _abbrechen = abbrechen;
    }

    private readonly Action? _abbrechen;
    private bool _abbruchAngefordert;
    public bool IstAbbrechbar => _abbrechen is not null;
    private bool KannAbbrechen() => IstAbbrechbar && !_abbruchAngefordert;

    [RelayCommand(CanExecute = nameof(KannAbbrechen))]
    private void Abbrechen()
    {
        _abbruchAngefordert = true;
        AbbrechenCommand.NotifyCanExecuteChanged();
        DateienText = "Import wird abgebrochen …";
        _abbrechen?.Invoke();
    }

    /// <summary>Fenstertitel.</summary>
    public string Titel { get; }

    /// <summary>Fortschritt in Prozent (0–100) für den Balken.</summary>
    [ObservableProperty]
    private double _prozent;

    /// <summary>Text „Dateien: x von y“.</summary>
    [ObservableProperty]
    private string _dateienText = "Dateien werden gezählt …";

    /// <summary>Text „Datenmenge: x von y MB“.</summary>
    [ObservableProperty]
    private string _mengeText = string.Empty;

    /// <summary>
    /// Übernimmt einen neuen Zwischenstand. Wird über <see cref="Progress{T}"/> im Oberflächen-Thread aufgerufen.
    /// </summary>
    /// <param name="stand">Der gemeldete Zwischenstand.</param>
    public void Aktualisiere(KopierFortschritt stand)
    {
        if (_abbruchAngefordert) return;

        // Anteil nach Datenmenge, weil große Dateien länger dauern; ohne Datenmenge (nur leere Dateien) nach Anzahl
        Prozent = stand.GesamtBytes > 0
            ? 100.0 * stand.KopierteBytes / stand.GesamtBytes
            : 100.0 * stand.KopierteDateien / Math.Max(1, stand.GesamtDateien);

        CultureInfo de = CultureInfo.GetCultureInfo("de-DE");
        DateienText = string.Format(de, "Dateien: {0:N0} von {1:N0}", stand.KopierteDateien, stand.GesamtDateien);
        // Ohne bekannte Datenmenge (z. B. beim Lesen der Aufzeichnungen eines Geräts) entfällt die Mengenzeile
        MengeText = stand.GesamtBytes > 0
            ? string.Format(de, "Datenmenge: {0:N1} von {1:N1} MB", stand.KopierteBytes / BytesJeMegabyte, stand.GesamtBytes / BytesJeMegabyte)
            : string.Empty;
    }
}
