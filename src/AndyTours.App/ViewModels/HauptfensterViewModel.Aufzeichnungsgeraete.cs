using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using AndyTours.Import.Garmin;
using AndyTours.Daten;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Data.Sqlite;

namespace AndyTours.App.ViewModels;

/// <summary>Filtert die Tourenansicht nach dem im FIT gespeicherten Aufzeichnungsgerät (Modell).</summary>
public partial class HauptfensterViewModel
{
    private const string AlleAufzeichnungsgeraete = "Alle Geräte";
    private const string UnbekanntesAufzeichnungsgeraet = "Unbekannt";
    private readonly Dictionary<long, string> _aufzeichnungsgeraete = new();
    private bool _geraeteWerdenGefuellt;

    /// <summary>Modelle des gesamten Datenbestands einschließlich Touren ohne Geräteinformation.</summary>
    public ObservableCollection<string> Aufzeichnungsgeraete { get; } = [AlleAufzeichnungsgeraete];

    /// <summary>Nur ein Anzeigefilter: gespeicherte Touren und Downloads werden nicht verändert.</summary>
    [ObservableProperty]
    private string _ausgewaehltesAufzeichnungsgeraet = AlleAufzeichnungsgeraete;

    /// <summary>Berechnet Tabelle und Tabellensummen mit dem neuen Gerätefilter.</summary>
    partial void OnAusgewaehltesAufzeichnungsgeraetChanged(string value)
    {
        if (!_geraeteWerdenGefuellt)
        {
            AktualisiereSummen(listenNeu: false);
        }
    }

    /// <summary>Fehlende Angaben bleiben ausdrücklich unbekannt; sie werden keinem Modell zugeordnet.</summary>
    private string Aufzeichnungsgeraet(long id) =>
        _aufzeichnungsgeraete.TryGetValue(id, out string? name) && !string.IsNullOrWhiteSpace(name)
            ? name : UnbekanntesAufzeichnungsgeraet;

    /// <summary>Liest die dauerhaft mit der Tour gespeicherte Geräteinformation und erhält die Auswahl.</summary>
    private void AktualisiereAufzeichnungsgeraete()
    {
        using SqliteConnection verbindung = OeffneDatenbank.Ausfuehren(Datenbankdatei);
        foreach ((string key, string name) in EinstellungTabelle.LesenMitVorsilbe(verbindung, Einstellungsschluessel.GarminGeraetVorsilbe))
        {
            if (long.TryParse(key.AsSpan(Einstellungsschluessel.GarminGeraetVorsilbe.Length),
                    NumberStyles.None, CultureInfo.InvariantCulture, out long id))
            {
                _aufzeichnungsgeraete[id] = name;
            }
        }

        // Alte Touren haben noch keinen Metadateneintrag: vorhandene FIT-Dateien einmalig
        // pro geöffnetem Datenbestand auswerten. Fehlende Dateien bleiben unbekannt.
        string wurzel = Path.GetFullPath(Datenordner) + Path.DirectorySeparatorChar;
        foreach (var einheit in _bestand.Trainingseinheiten.Where(e => !_aufzeichnungsgeraete.ContainsKey(e.Id)))
        {
            string name = UnbekanntesAufzeichnungsgeraet;
            foreach (string relativ in einheit.Anhaenge.Select(a => a.RelativerPfad)
                         .Where(p => p.EndsWith(".fit", StringComparison.OrdinalIgnoreCase)).Distinct())
            {
                try
                {
                    string pfad = Path.GetFullPath(Path.Combine(Datenordner, relativ));
                    if (!pfad.StartsWith(wurzel, StringComparison.OrdinalIgnoreCase) || !File.Exists(pfad))
                    {
                        continue;
                    }
                    var (aufzeichnung, _) = LeseFitAufzeichnung.Ausfuehren(pfad);
                    if (aufzeichnung is not null && !string.IsNullOrWhiteSpace(aufzeichnung.Geraet))
                    {
                        name = aufzeichnung.Geraet;
                        break;
                    }
                }
                catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    // Ein fehlender oder ungültiger Anhang darf die Tourenansicht nicht verhindern.
                }
            }
            _aufzeichnungsgeraete[einheit.Id] = name;
        }

        string gewaehlt = AusgewaehltesAufzeichnungsgeraet;
        _geraeteWerdenGefuellt = true;
        try
        {
            Aufzeichnungsgeraete.Clear();
            Aufzeichnungsgeraete.Add(AlleAufzeichnungsgeraete);
            foreach (string name in _bestand.Trainingseinheiten.Select(e => Aufzeichnungsgeraet(e.Id))
                         .Distinct().OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase))
            {
                Aufzeichnungsgeraete.Add(name);
            }
            AusgewaehltesAufzeichnungsgeraet = Aufzeichnungsgeraete.Contains(gewaehlt) ? gewaehlt : AlleAufzeichnungsgeraete;
            OnPropertyChanged(nameof(AusgewaehltesAufzeichnungsgeraet));
        }
        finally
        {
            _geraeteWerdenGefuellt = false;
        }
    }
}
