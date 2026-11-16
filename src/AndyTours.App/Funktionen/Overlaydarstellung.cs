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

    private static readonly Dictionary<Overlayfarbe, (string Name, byte R, byte G, byte B)> Farben = new()
    {
        [Overlayfarbe.Violett] = ("Violett", 0x9B, 0x30, 0xD9),
        [Overlayfarbe.Blau] = ("Blau", 0x1F, 0x4E, 0xD8),
        [Overlayfarbe.Hellblau] = ("Hellblau", 0x3B, 0xA7, 0xF0),
        [Overlayfarbe.Tuerkis] = ("Türkis", 0x00, 0xA8, 0xA8),
        [Overlayfarbe.Gruen] = ("Grün", 0x2E, 0x9E, 0x3A),
        [Overlayfarbe.Gelb] = ("Gelb", 0xE8, 0xC2, 0x00),
        [Overlayfarbe.Orange] = ("Orange", 0xF0, 0x7A, 0x10),
        [Overlayfarbe.Rot] = ("Rot", 0xD6, 0x22, 0x22),
        [Overlayfarbe.Pink] = ("Pink", 0xE8, 0x3E, 0x9C),
        [Overlayfarbe.Braun] = ("Braun", 0x8B, 0x5A, 0x2B),
        [Overlayfarbe.Grau] = ("Grau", 0x80, 0x80, 0x80),
        [Overlayfarbe.Schwarz] = ("Schwarz", 0x20, 0x20, 0x20),
    };

    public static string Anzeigename(Overlayfarbe farbe) => Farben[farbe].Name;

    public static System.Windows.Media.Color Farbwert(Overlayfarbe farbe)
    {
        (_, byte r, byte g, byte b) = Farben[farbe];
        return System.Windows.Media.Color.FromRgb(r, g, b);
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
            (_, byte r, byte g, byte b) = Farben[overlay.Farbe ?? gemeinsameFarbe];
            var farbe = Mapsui.Styles.Color.FromArgb(Deckkraft, r, g, b);
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
