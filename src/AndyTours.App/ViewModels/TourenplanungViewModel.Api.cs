using AndyTours.Core.Berechnungen;
using AndyTours.Application.Planung;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.App.ViewModels;

public partial class TourenplanungViewModel
{
    internal Func<Fahrprofil, Geoposition, Geoposition, CancellationToken, Task<PlanungsRoutingAntwort>>? ApiRouting { get; set; }
    internal Func<Task>? ApiSpeichern { get; set; }
    internal Func<Task>? ApiOeffnen { get; set; }
    internal Func<Task>? ApiGarminUebertragen { get; set; }
    internal Action<Fahrprofil>? ApiProfilGeaendert { get; set; }
    public bool IstApiModus => ApiRouting is not null;

    private bool _apiDateiBeschaeftigt;
    public bool ApiDateiBeschaeftigt
    {
        get => _apiDateiBeschaeftigt;
        set { if (SetProperty(ref _apiDateiBeschaeftigt, value)) OnPropertyChanged(nameof(KannApiBedienen)); }
    }
    private string _apiProfilHinweis = string.Empty;
    public string ApiProfilHinweis { get => _apiProfilHinweis; set => SetProperty(ref _apiProfilHinweis, value); }
    public bool KannApiBedienen => !ApiDateiBeschaeftigt;

    internal void UebernimmApiPlanung(Tourenplanung planung, string localPfad)
    {
        BeendeErmittlung();
        _wirdGeladen = true;
        try
        {
            Profil = planung.Profil;
            Uebernimm(planung);
            Dateipfad = localPfad;
            IstGeaendert = false;
            MerkeOrdner(localPfad);
            GanzePlanungAngefordert?.Invoke(this, EventArgs.Empty);
        }
        finally { _wirdGeladen = false; }
    }

    internal void BestaetigeApiSpeichern(Tourenplanung snapshot, string localPfad)
    {
        Dateipfad = localPfad;
        MerkeOrdner(localPfad);
        if (ReferenceEquals(Planung, snapshot)) IstGeaendert = false;
    }

    private async Task<Teilstueck?> ErmittleApiTeilstueck(Fahrprofil profil, Geoposition von, Geoposition nach, CancellationToken cancellationToken)
    {
        var callback = ApiRouting;
        if (callback is null) return null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await callback(profil, von, nach, cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            var dto = new PlanungsStand(ToContract(profil), [new(von.Breite, von.Laenge), new(nach.Breite, nach.Laenge)],
                [new(response.Punkte, response.Abbiegepunkte, response.IstLuftlinie)]);
            var segment = PlanungsVertragMapper.ToCore(dto, cancellationToken).Teilstuecke[0];
            if (segment?.IstLuftlinie == true)
                Hinweis = response.Warncode switch
                {
                    "routing_einstellungen_unvollstaendig" => EinstellungenUnvollstaendig,
                    "routing_nicht_erreichbar" => DienstNichtErreichbar,
                    _ => "Die Strecke wurde nicht ermittelt und wird als Luftlinie angezeigt."
                };
            return segment;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            cancellationToken.ThrowIfCancellationRequested();
            Hinweis = "Die Strecke konnte nicht ermittelt werden; sie wird als Luftlinie angezeigt.";
            return ErstelleLuftlinie.Ausfuehren(von, nach);
        }
    }

    private static CourseFahrprofil ToContract(Fahrprofil profil) => profil switch
    {
        Fahrprofil.Rennrad => CourseFahrprofil.Rennrad,
        Fahrprofil.Trekking => CourseFahrprofil.Trekking,
        Fahrprofil.Mountainbike => CourseFahrprofil.Mountainbike,
        _ => throw new ArgumentOutOfRangeException(nameof(profil))
    };
}
