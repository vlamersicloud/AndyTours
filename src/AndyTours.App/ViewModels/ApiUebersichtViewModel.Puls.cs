using AndyTours.Application.Auswertung;
using AndyTours.Application.Stammdaten;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using CommunityToolkit.Mvvm.ComponentModel;
using OxyPlot;

namespace AndyTours.App.ViewModels;

public partial class ApiUebersichtViewModel
{
    private readonly IPulsAuswertung? _puls;
    private PulsReferenzAntwort? _pulsReferenz;
    private long _pulsGeneration;
    private bool _pulsBeendet;

    [ObservableProperty] private IReadOnlyList<PulsprofilAntwort> _pulsprofile = [];
    [ObservableProperty] private PulsprofilAntwort? _pulsprofil;
    [ObservableProperty] private bool _bpmBereiche;
    [ObservableProperty] private string _pulsReferenzText = "Keine Garmin-Referenz geladen.";
    [ObservableProperty] private string _pulsStatus = "Pulsauswertung laden.";
    [ObservableProperty] private PlotModel _pulsDiagramm = new() { Background = OxyColors.White };
    [ObservableProperty] private IReadOnlyList<ApiPulszeile> _pulszeilen = [];

    public ApiUebersichtViewModel(IAktivitaetsauswertung auswertung, ITourenSpeicher touren,
        IStammdatenLeser stammdatenLeser, IPulsAuswertung puls)
        : this(auswertung, touren, stammdatenLeser)
    {
        _puls = puls ?? throw new ArgumentNullException(nameof(puls));
    }

    partial void OnPulsprofilChanged(PulsprofilAntwort? value) => LeerePulsanzeige();
    partial void OnBpmBereicheChanged(bool value) => LeerePulsanzeige();

    private void LeerePulsanzeige()
    {
        _pulsGeneration++;
        Pulszeilen = [];
        PulsDiagramm.Series.Clear();
        PulsDiagramm.Axes.Clear();
        PulsDiagramm.Legends.Clear();
        PulsDiagramm.InvalidatePlot(true);
        PulsStatus = "Auswahl geändert. Pulsauswertung bitte neu laden.";
    }

    private void UebernimmPulsreferenz(PulsReferenzAntwort antwort, string? vorherigerSport = null, bool waehleStandard = true)
    {
        ArgumentNullException.ThrowIfNull(antwort);
        _pulsReferenz = antwort;
        Pulsprofile = antwort.Profile.ToArray();
        Pulsprofil = vorherigerSport is null
            ? (waehleStandard
                ? Pulsprofile.FirstOrDefault(profile => profile.Sport == "CYCLING")
                  ?? Pulsprofile.FirstOrDefault(profile => profile.Sport == "DEFAULT")
                : null)
            : Pulsprofile.FirstOrDefault(profile => profile.Sport == vorherigerSport);
        PulsReferenzText = antwort.Abgerufen is DateTimeOffset abgerufen
            ? $"Garmin-Referenz vom {abgerufen.ToLocalTime():dd.MM.yyyy HH:mm}; aktuell, nicht historisch."
            : "Keine Garmin-Referenz geladen. Ohne Profil werden 10-bpm-Bereiche ausgewertet.";
        if (vorherigerSport is not null && Pulsprofil is null)
            PulsStatus = "Das zuvor gewählte Sportprofil ist nicht mehr vorhanden. Bitte bewusst neu wählen.";
    }

    private bool KannPulsLaden() => _puls is not null && !Laedt && !EditorOffen && !_pulsBeendet;
}
