using AndyTours.App.Funktionen;
using AndyTours.Contracts.V1;

namespace AndyTours.App.ViewModels;

public sealed record ApiOverlayFarboption(OverlayFarbe? Farbe, string Name, string Pinsel);

public static class ApiOverlayFarboptionen
{
    public static IReadOnlyList<ApiOverlayFarboption> Alle() => Enum.GetValues<OverlayFarbe>()
        .Select(farbe =>
        {
            var core = ApiOverlayMapper.Farbe(farbe);
            var ton = Overlayfarben.Wert(core);
            return new ApiOverlayFarboption(farbe, ton.Name, ton.Hex);
        }).ToArray();

    public static ApiOverlayFarboption Gemeinsam(OverlayFarbe gemeinsam)
    {
        var ton = Overlayfarben.Wert(ApiOverlayMapper.Farbe(gemeinsam));
        return new ApiOverlayFarboption(null, $"Gemeinsame Farbe ({ton.Name})", ton.Hex);
    }
}
