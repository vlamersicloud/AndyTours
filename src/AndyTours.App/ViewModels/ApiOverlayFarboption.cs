using AndyTours.App.Funktionen;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public sealed record ApiOverlayFarboption(OverlayFarbe? Farbe, string Name, string Pinsel);

public static class ApiOverlayFarboptionen
{
    public static IReadOnlyList<ApiOverlayFarboption> Alle() => Enum.GetValues<OverlayFarbe>()
        .Select(farbe => { var tone = Overlayfarben.Wert(ApiOverlayMapper.Farbe(farbe)); return new ApiOverlayFarboption(farbe, tone.Name, tone.Hex); }).ToArray();
    public static ApiOverlayFarboption Gemeinsam(OverlayFarbe gemeinsam)
    {
        var tone = Overlayfarben.Wert(ApiOverlayMapper.Farbe(gemeinsam));
        return new ApiOverlayFarboption(null, $"Gemeinsame Farbe ({tone.Name})", tone.Hex);
    }
    public static string FarbeHex(OverlayFarbe farbe) => Overlayfarben.Wert(ApiOverlayMapper.Farbe(farbe)).Hex;
}
