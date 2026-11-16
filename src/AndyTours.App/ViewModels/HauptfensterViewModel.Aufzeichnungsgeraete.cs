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
    private const string UnbekanntesAufzeichnungsgeraet = "Unbekannt";
    private readonly Dictionary<long, string> _aufzeichnungsgeraete = new();
    private bool _geraeteWerdenGefuellt;

    /// <summary>Mehrfachauswahl; ohne Häkchen werden alle Geräte angezeigt.</summary>
    public ObservableCollection<AufzeichnungsgeraetAuswahl> Aufzeichnungsgeraete { get; } = [];

    private void GeraeteauswahlGeaendert(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_geraeteWerdenGefuellt && e.PropertyName == nameof(AufzeichnungsgeraetAuswahl.IstAusgewaehlt))
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

        HashSet<string> gewaehlt = Aufzeichnungsgeraete.Where(g => g.IstAusgewaehlt)
            .Select(g => g.Name).ToHashSet();
        _geraeteWerdenGefuellt = true;
        try
        {
            foreach (var geraet in Aufzeichnungsgeraete)
            {
                geraet.PropertyChanged -= GeraeteauswahlGeaendert;
            }
            Aufzeichnungsgeraete.Clear();
            foreach (string name in _bestand.Trainingseinheiten.Select(e => Aufzeichnungsgeraet(e.Id))
                         .Distinct().OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase))
            {
                var geraet = new AufzeichnungsgeraetAuswahl(name) { IstAusgewaehlt = gewaehlt.Contains(name) };
                geraet.PropertyChanged += GeraeteauswahlGeaendert;
                Aufzeichnungsgeraete.Add(geraet);
            }
        }
        finally
        {
            _geraeteWerdenGefuellt = false;
        }
    }
}


/// <summary>Ein unabhängig auswählbares Gerätemodell im Anzeigefilter.</summary>
public partial class AufzeichnungsgeraetAuswahl(string name) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    private bool _istAusgewaehlt;
}
