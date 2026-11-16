using Mapsui;
using Mapsui.Layers;
using Mapsui.Styles;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;

namespace AndyTours.App.Funktionen;

/// <summary>Wie Overlays aussehen: Palette und Overlay-Ebene auf Karten.</summary>
public static class Overlaydarstellung
{
    private const string Ebenenname = "Overlays";
    private const int Deckkraft = 190;
    private const double Linienbreite = 3;
    private const double Rand = 0.08;

    public static string Anzeigename(Overlayfarbe farbe) => Overlayfarben.Wert(farbe).Name;

    public static System.Windows.Media.Color Farbwert(Overlayfarbe farbe)
    {
        var tone = Overlayfarben.Wert(farbe);
        return System.Windows.Media.Color.FromRgb(tone.R, tone.G, tone.B);
    }

    public static MemoryLayer ErzeugeEbene() => new(Ebenenname) { Style = null };

    /// <summary>Legacy adapter preserving the local database-backed overlay source.</summary>
    public static void Fuelle(Map karte) => Fuelle(karte, Overlaybestand.Eingeschaltete, Overlaybestand.Farbe);

    /// <summary>Renders an explicit immutable overlay snapshot without consulting global/local overlay state.</summary>
    public static void Fuelle(Map karte, IReadOnlyList<Overlay> overlays, Overlayfarbe gemeinsameFarbe)
    {
        var ebene = (MemoryLayer)karte.Layers.First(e => e.Name == Ebenenname);
        ebene.Features = [.. overlays.SelectMany(overlay =>
        {
            var tone = Overlayfarben.Wert(overlay.Farbe ?? gemeinsameFarbe);
            var farbe = Mapsui.Styles.Color.FromArgb(Deckkraft, tone.R, tone.G, tone.B);
            return overlay.Abschnitte.Select(abschnitt => Linie(abschnitt, farbe));
        })];
        karte.Refresh();
    }

    public static void ZeigeGanz(Map karte, Overlay overlay)
    {
        (Geoposition suedWest, Geoposition nordOst) = UmschliesseOverlay.Ausfuehren(overlay);
        karte.Navigator.ZoomToBox(Kartenzeichnung.Rechteck(suedWest, nordOst, Rand), MBoxFit.Fit);
    }

    private static IFeature Linie(IReadOnlyList<Geoposition> abschnitt, Mapsui.Styles.Color farbe)
    {
        if (abschnitt.Count > 1) return Kartenzeichnung.Linie(abschnitt, new Pen(farbe, Linienbreite));
        return new PointFeature(Kartenzeichnung.InKartenkoordinaten(abschnitt[0]))
        {
            Styles = [new SymbolStyle { SymbolType = SymbolType.Ellipse, SymbolScale = 0.3, Fill = new Mapsui.Styles.Brush(farbe), Outline = null }],
        };
    }
}
