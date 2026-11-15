using System.IO;
using System.Net.Http;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Legends;
using AndyTours.App.Funktionen;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Daten.Datenordner;
using AndyTours.Import.Garmin;

namespace AndyTours.App.ViewModels;

/// <summary>Pulsbereiche nach Tag, ISO-Woche, Monat oder Jahr; Garmin-Grenzen sind ausdrücklich aktuelle Referenzwerte.</summary>
public partial class JahresfortschrittViewModel
{
    [ObservableProperty] private IReadOnlyList<GarminPulsprofil> _pulsprofile = [];
    [ObservableProperty] private GarminPulsprofil? _pulsprofil;
    [ObservableProperty] private string _pulsReferenz = "Keine Garmin-Referenz geladen. Ohne Profil werden 10-bpm-Bereiche ausgewertet.";
    [ObservableProperty] private string _pulsStatus = "Pulsanalyse für den gewählten Zeitraum laden.";
    [ObservableProperty] private IReadOnlyList<PulsAuswertungszeile> _pulszeilen = [];
    [ObservableProperty] private bool _bpmBereiche;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GarminZonenAktualisierenCommand))]
    [NotifyCanExecuteChangedFor(nameof(PulsdatenLadenCommand))]
    private bool _istBeschaeftigt;

    /// <summary>Gestapelte Bereichszeiten in Minuten; keine Mittelwert-Zuordnung ganzer Touren.</summary>
    public PlotModel PulsDiagramm { get; } = new() { Culture = Deutsch, PlotAreaBorderColor = OxyColors.LightGray };
    private bool KannLaden() => !IstBeschaeftigt;

    partial void OnPulsprofilChanged(GarminPulsprofil? value) { _generation++; LeerePulsanalyse(); }
    partial void OnBpmBereicheChanged(bool value) { _generation++; LeerePulsanalyse(); }

    /// <summary>Entfernt Ergebnisse nach Filter-/Profilwechsel, statt alte Werte unter einer neuen Auswahl zu zeigen.</summary>
    private void LeerePulsanalyse()
    {
        Pulszeilen = [];
        PulsDiagramm.Series.Clear(); PulsDiagramm.Axes.Clear(); PulsDiagramm.Legends.Clear();
        PulsDiagramm.InvalidatePlot(true);
        PulsStatus = "Pulsanalyse für den gewählten Zeitraum neu laden.";
    }

    /// <summary>Lädt die Referenz des Datenordners; ein Profil wird nie stillschweigend aus einer anderen Sportart übernommen.</summary>
    private void LadeGespeicherteZonen()
    {
        GarminPulszonenStand? stand = null;
        try
        {
            using SqliteConnection db = OeffneDatenbank.Ausfuehren(Path.Combine(_datenordner, DatenordnerInhalt.Datenbankdatei));
            stand = GarminPulszonenEinstellung.Laden(db);
        }
        catch (Exception f) when (f is SqliteException or IOException or UnauthorizedAccessException) { }
        UebernimmZonen(stand);
    }

    /// <summary>Sportwahl bleibt sichtbar; für Radfahrdaten wird CYCLING, sonst nur das allgemeine DEFAULT-Profil vorausgewählt.</summary>
    private void UebernimmZonen(GarminPulszonenStand? stand)
    {
        Pulsprofile = stand?.Profile ?? [];
        Pulsprofil = Pulsprofile.FirstOrDefault(p => p.Sport == "CYCLING") ?? Pulsprofile.FirstOrDefault(p => p.Sport == "DEFAULT");
        PulsReferenz = stand is null ? "Keine Garmin-Referenz geladen. Ohne Profil werden 10-bpm-Bereiche ausgewertet."
            : $"Garmin-Referenz vom Abruf {stand.Abgerufen.ToLocalTime():dd.MM.yyyy HH:mm}. Keine historischen Zonengrenzen. Sportprofil bewusst wählen.";
    }

    /// <summary>Ruft nur beim ausdrücklichen Klick die konfigurierten aktuellen Garmin-Zonen ab.</summary>
    [RelayCommand(CanExecute = nameof(KannLaden))]
    private async Task GarminZonenAktualisierenAsync()
    {
        string ordner = _datenordner;
        if (string.IsNullOrWhiteSpace(ordner)) { return; }
        IstBeschaeftigt = true;
        try
        {
            using SqliteConnection db = OeffneDatenbank.Ausfuehren(Path.Combine(ordner, DatenordnerInhalt.Datenbankdatei));
            string? key = GarminEinstellung.Laden(db);
            if (string.IsNullOrWhiteSpace(key))
            {
                PulsStatus = "Bitte zuerst unter Einstellungen → Persönlich den Garmin-Key speichern.";
                return;
            }

            using var handler = new HttpClientHandler { AllowAutoRedirect = false };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
            PulsStatus = "Aktuelle Garmin-Pulsprofile werden geladen …";
            IReadOnlyList<GarminPulsprofil> profile = await new GarminPersonalClient(http, key).LesePulsprofileAsync();
            if (_datenordner != ordner) { return; }
            if (profile.Count == 0) { PulsStatus = "Garmin liefert keine konfigurierten Zonen. Bestehende Referenz wurde nicht überschrieben."; return; }
            var stand = new GarminPulszonenStand(DateTimeOffset.UtcNow, profile);
            GarminPulszonenEinstellung.Speichern(db, stand);
            UebernimmZonen(stand);
            PulsStatus = "Garmin-Referenz aktualisiert. Sportprofil prüfen und Pulsanalyse laden.";
        }
        catch (Exception f) when (f is HttpRequestException or OperationCanceledException or JsonException or IOException
            or InvalidDataException or InvalidOperationException or SqliteException or UnauthorizedAccessException)
        {
            PulsStatus = f is OperationCanceledException ? "Zeitlimit beim Garmin-Abruf. Bisherige Referenz bleibt erhalten."
                : "Garmin-Zonen konnten nicht aktualisiert werden: " + f.Message;
        }
        finally { IstBeschaeftigt = false; }
    }

    /// <summary>Liest die ausgewählten FIT-Dateien im Hintergrund; Resultate eines inzwischen geänderten Filters werden verworfen.</summary>
    [RelayCommand(CanExecute = nameof(KannLaden))]
    private async Task PulsdatenLadenAsync()
    {
        if (Von is not DateOnly von || Bis is not DateOnly bis || von > bis)
        { PulsStatus = "Bitte zuerst einen gültigen Zeitraum einstellen."; return; }
        var daten = Auswahl().Where(e => e.Datum >= von && e.Datum <= bis).ToList();
        IReadOnlyList<Aktivitaetsperiode> perioden;
        try { perioden = BerechneAktivitaetsperioden.Ausfuehren(daten, von, bis, Gruppierung); }
        catch (ArgumentException f) { PulsStatus = f.Message; return; }
        int generation = _generation;
        string ordner = _datenordner;
        Zeitraster raster = Gruppierung;
        GarminPulsprofil? profil = BpmBereiche ? null : Pulsprofil;
        IstBeschaeftigt = true;
        var fortschritt = new Progress<string>(text => { if (_generation == generation && IstBeschaeftigt) { PulsStatus = text; } });
        try
        {
            var ergebnis = await Task.Run(() => LesePulsstatistik.Ausfuehren(daten, ordner, raster, profil, fortschritt));
            if (_generation != generation || _datenordner != ordner)
            { PulsStatus = "Auswahl wurde geändert. Pulsanalyse bitte erneut laden."; return; }
            ZeichnePulszeiten(perioden, ergebnis.Zeiten, profil);
            double gemessen = ergebnis.Zeiten.Values.Sum();
            PulsStatus = $"{daten.Count} Aktivitäten; {ergebnis.OhneDaten} ohne auswertbare FIT-Pulsmesszeit. "
                + $"Auswertbare Messzeit {TimeSpan.FromSeconds(gemessen).TotalHours.ToString("N2", Deutsch)} h, "
                + $"Fahrzeit laut Tourdaten {TimeSpan.FromSeconds(ergebnis.Fahrzeit).TotalHours.ToString("N2", Deutsch)} h. "
                + "Fehlwerte, Pausen und Lücken über 60 s werden nicht aufgefüllt; Anteile beziehen sich nur auf Messzeit. "
                + (profil is null ? "10-bpm-Bereiche, keine Garmin-Zuordnung." : $"Profil {profil.Anzeige}: aktuelle Referenz, nicht historische Garmin-Zonen.");
        }
        catch (Exception f) when (f is IOException or UnauthorizedAccessException or InvalidOperationException)
        { PulsStatus = "Pulsanalyse fehlgeschlagen: " + f.Message; }
        finally { IstBeschaeftigt = false; }
    }

    /// <summary>Stellt Bereichszeiten je Periode gestapelt dar und nennt dieselben bpm-Grenzen in der Tabelle.</summary>
    private void ZeichnePulszeiten(IReadOnlyList<Aktivitaetsperiode> perioden,
        IReadOnlyDictionary<(DateOnly Periode, int Bereich), double> zeiten, GarminPulsprofil? profil)
    {
        PulsDiagramm.Series.Clear(); PulsDiagramm.Axes.Clear(); PulsDiagramm.Legends.Clear();
        var achse = new CategoryAxis { Position = AxisPosition.Bottom, Minimum = -.5, Maximum = perioden.Count - .5,
            Angle = perioden.Count > 16 ? -45 : 0, IsZoomEnabled = false, IsPanEnabled = false };
        achse.Labels.AddRange(perioden.Select(p => p.Beschriftung));
        PulsDiagramm.Axes.Add(achse);
        PulsDiagramm.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Minuten", Minimum = 0,
            IsZoomEnabled = false, IsPanEnabled = false, MajorGridlineStyle = LineStyle.Dot });
        PulsDiagramm.Legends.Add(new Legend { LegendPosition = LegendPosition.BottomCenter,
            LegendPlacement = LegendPlacement.Outside, LegendOrientation = LegendOrientation.Horizontal });
        var farben = new[] { OxyColors.Gray, OxyColors.SteelBlue, OxyColors.ForestGreen, OxyColors.Goldenrod, OxyColors.OrangeRed, OxyColors.Purple };
        int[] bereiche = zeiten.Keys.Select(k => k.Bereich).Distinct().OrderBy(z => z).ToArray();
        var summen = zeiten.GroupBy(z => z.Key.Periode).ToDictionary(g => g.Key, g => g.Sum(z => z.Value));
        double[] unterkante = new double[perioden.Count];
        var zeilen = new List<PulsAuswertungszeile>();
        foreach (int bereich in bereiche)
        {
            string zone = profil is null ? "ohne Garmin-Zuordnung" : bereich == 0 ? "Unter Zone 1" : $"Garmin Zone {bereich}";
            string bpm = profil?.Bereich(bereich) ?? $"{bereich}–{bereich + 9} bpm";
            var serie = new RectangleBarSeries { Title = profil is null ? bpm : zone + " · " + bpm,
                FillColor = farben[profil is null ? (bereich / 10) % farben.Length : bereich], StrokeThickness = 0,
                LabelFormatString = null, TrackerFormatString = "{7}" };
            for (int i = 0; i < perioden.Count; i++)
            {
                DateOnly key = BerechneAktivitaetsperioden.Beginn(perioden[i].Von, Gruppierung);
                double sekunden = zeiten.GetValueOrDefault((key, bereich));
                if (sekunden <= 0) { continue; }
                double alle = summen.GetValueOrDefault(key);
                serie.Items.Add(new RectangleBarItem(i - .35, unterkante[i], i + .35, unterkante[i] + sekunden / 60)
                { Title = perioden[i].Beschriftung + " · " + zone + " · " + bpm + ": " + (sekunden / 60).ToString("N1", Deutsch) + " min" });
                unterkante[i] += sekunden / 60;
                zeilen.Add(new PulsAuswertungszeile(perioden[i].Beschriftung, zone, bpm,
                    TimeSpan.FromSeconds(sekunden).TotalMinutes.ToString("N1", Deutsch) + " min",
                    (sekunden / alle).ToString("P1", Deutsch)));
            }
            PulsDiagramm.Series.Add(serie);
        }

        var reihenfolge = perioden.Select((p, i) => (p.Beschriftung, Index: i)).ToDictionary(p => p.Beschriftung, p => p.Index);
        Pulszeilen = zeilen.OrderBy(z => reihenfolge[z.Zeitraum]).ToArray();
        PulsDiagramm.InvalidatePlot(true);
    }
}
