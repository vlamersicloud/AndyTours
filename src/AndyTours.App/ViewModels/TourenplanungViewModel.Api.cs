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
    public bool IstApiModus => ApiRouting is not null;

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
            return PlanungsVertragMapper.ToCore(dto, cancellationToken).Teilstuecke[0];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
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
