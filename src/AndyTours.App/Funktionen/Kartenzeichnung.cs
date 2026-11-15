using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using NetTopologySuite.Geometries;
using AndyTours.Core.Modell;

namespace AndyTours.App.Funktionen;

/// <summary>
/// Gemeinsame Bausteine der Karten (Tourdetail, Tourenplanung, Overlays): Karte anlegen, Koordinaten umrechnen,
/// Linien, Zeichen und Ausschnitte bilden.
/// </summary>
/// <remarks>
/// Die Karte rechnet in Web-Mercator (Meter); AndyTours speichert Positionen in Grad (<see cref="Geoposition"/>).
/// </remarks>
public static class Kartenzeichnung
{
    /// <summary>Kleinste Breite bzw. Höhe eines Ausschnitts in Kartenmetern, damit kurze Strecken nicht bis zum Anschlag vergrößert werden.</summary>
    private const double Mindestgroesse = 500;

    /// <summary>
    /// Legt eine leere Karte an: ohne die Widgets, die Mapsui von sich aus einblendet (Protokoll, Leistung), und ohne
    /// Drehen.
    /// </summary>
    /// <returns>Die Karte.</returns>
    public static Map ErzeugeKarte()
    {
        var karte = new Map();
        karte.Widgets.Clear();
        karte.Navigator.RotationLock = true;
        return karte;
    }

    /// <summary>Rechnet eine Position in Grad in die Koordinaten der Karte (Web-Mercator) um.</summary>
    /// <param name="position">Die Position.</param>
    /// <returns>Der Punkt in Kartenkoordinaten.</returns>
    public static MPoint InKartenkoordinaten(Geoposition position) =>
        SphericalMercator.FromLonLat(new MPoint(position.Laenge, position.Breite));

    /// <summary>Rechnet einen Punkt in Kartenkoordinaten (Web-Mercator) in eine Position in Grad um.</summary>
    /// <param name="kartenpunkt">Der Punkt in Kartenkoordinaten.</param>
    /// <returns>Die Position.</returns>
    public static Geoposition InGeoposition(MPoint kartenpunkt)
    {
        (double laenge, double breite) = SphericalMercator.ToLonLat(kartenpunkt.X, kartenpunkt.Y);
        return new Geoposition(breite, laenge);
    }

    /// <summary>
    /// Eine Linie durch die Positionen; aus nur einer Position wird ein Punkt.
    /// </summary>
    /// <param name="positionen">Die Positionen, mindestens eine.</param>
    /// <param name="stift">Farbe, Breite und Strichart.</param>
    /// <returns>Das Kartenelement.</returns>
    public static GeometryFeature Linie(IEnumerable<Geoposition> positionen, Pen stift)
    {
        Coordinate[] linie = [.. positionen.Select(InKartenkoordinaten).Select(p => new Coordinate(p.X, p.Y))];
        Geometry geometrie = linie.Length > 1 ? new LineString(linie) : new NetTopologySuite.Geometries.Point(linie[0]);
        return new GeometryFeature(geometrie) { Styles = [new VectorStyle { Line = stift }] };
    }

    /// <summary>Ein Zeichen (Kreis, Quadrat …) an einer Position, z. B. Start, Ziel oder Wegpunkt.</summary>
    /// <param name="position">Die Position.</param>
    /// <param name="form">Form des Zeichens.</param>
    /// <param name="fuellung">Füllfarbe.</param>
    /// <param name="rand">Farbe des 2 px breiten Rands.</param>
    /// <param name="groesse">Größe (Maßstab des Mapsui-Symbols).</param>
    /// <returns>Das Kartenelement.</returns>
    public static PointFeature Zeichen(Geoposition position, SymbolType form, Color fuellung, Color rand, double groesse) =>
        new(InKartenkoordinaten(position))
        {
            Styles = [new SymbolStyle { SymbolType = form, SymbolScale = groesse, Fill = new Brush(fuellung), Outline = new Pen(rand, 2) }],
        };

    /// <summary>
    /// Ausschnitt um zwei Ecken, mit einem Rand und einer Mindestgröße von 500 m.
    /// </summary>
    /// <param name="suedWest">Südwestliche Ecke.</param>
    /// <param name="nordOst">Nordöstliche Ecke.</param>
    /// <param name="rand">Rand als Anteil der Breite bzw. Höhe je Seite, z. B. 0,1.</param>
    /// <returns>Der Ausschnitt in Kartenkoordinaten.</returns>
    public static MRect Rechteck(Geoposition suedWest, Geoposition nordOst, double rand)
    {
        MPoint sw = InKartenkoordinaten(suedWest), no = InKartenkoordinaten(nordOst);
        double breite = Math.Max(no.X - sw.X, Mindestgroesse), hoehe = Math.Max(no.Y - sw.Y, Mindestgroesse);
        MPoint mitte = new((sw.X + no.X) / 2, (sw.Y + no.Y) / 2);
        return new MRect(
            mitte.X - breite * (0.5 + rand), mitte.Y - hoehe * (0.5 + rand),
            mitte.X + breite * (0.5 + rand), mitte.Y + hoehe * (0.5 + rand));
    }
}
