using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using AndyTours.Core.Modell;

namespace AndyTours.Core.Berechnungen;

/// <summary>
/// Liest die Strecke einer beliebigen GPX-Datei für ein Overlay.
/// </summary>
/// <remarks>
/// Gelesen wird so:
/// <list type="bullet">
/// <item>Jedes Track-Segment (<c>trk/trkseg</c> mit <c>trkpt</c>) wird ein Abschnitt. Eine Planung aus „Tour planen“
/// ergibt so einen Abschnitt je Teilstück.</item>
/// <item>Enthält die Datei keine Trackpunkte, wird jede Route (<c>rte</c> mit <c>rtept</c>) ein Abschnitt.</item>
/// <item>Einzelne Wegpunkte (<c>wpt</c>) gehören nicht zur Strecke und werden übergangen.</item>
/// </list>
/// Die Namensräume werden nicht ausgewertet, damit GPX 1.0 und 1.1 gleichermaßen gelesen werden. Punkte ohne oder mit
/// ungültiger Breite bzw. Länge werden ausgelassen, leere Abschnitte ebenso.
/// </remarks>
public static class LeseGpxStrecke
{
    /// <summary>
    /// Liefert die Abschnitte der Strecke.
    /// </summary>
    /// <param name="text">Inhalt der GPX-Datei.</param>
    /// <returns>
    /// Die Abschnitte mit jeweils mindestens einem Punkt, oder <c>null</c>, wenn der Text kein lesbares XML ist oder
    /// keinen Track- bzw. Routenpunkt enthält.
    /// </returns>
    public static IReadOnlyList<IReadOnlyList<Geoposition>>? Ausfuehren(string text)
    {
        XElement? root;
        try { root = XDocument.Parse(text).Root; }
        catch (XmlException) { return null; }
        return ParseRoot(root, CancellationToken.None);
    }

    /// <summary>
    /// Loads GPX XML asynchronously from a caller-configured secure XmlReader.
    /// The caller must prohibit DTDs, set XmlResolver to null, and enforce input limits.
    /// </summary>
    public static async Task<IReadOnlyList<IReadOnlyList<Geoposition>>?> AusfuehrenAsync(
        XmlReader reader, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        cancellationToken.ThrowIfCancellationRequested();
        XDocument document;
        try { document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken).ConfigureAwait(false); }
        catch (XmlException) { return null; }
        cancellationToken.ThrowIfCancellationRequested();
        return ParseRoot(document.Root, cancellationToken);
    }

    private static IReadOnlyList<IReadOnlyList<Geoposition>>? ParseRoot(XElement? root, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (root is null || root.Name.LocalName != "gpx") return null;
        var sections = Abschnitte(Kinder(root, "trk").SelectMany(track => Kinder(track, "trkseg")), "trkpt", ct);
        if (sections.Count == 0) sections = Abschnitte(Kinder(root, "rte"), "rtept", ct);
        return sections.Count == 0 ? null : sections;
    }

    /// <summary>Die nicht leeren Abschnitte aus den Punkten der angegebenen Art je Behälter (Segment oder Route).</summary>
    private static List<IReadOnlyList<Geoposition>> Abschnitte(IEnumerable<XElement> containers, string pointName, CancellationToken ct)
    {
        var sections = new List<IReadOnlyList<Geoposition>>();
        foreach (var container in containers)
        {
            ct.ThrowIfCancellationRequested();
            var points = new List<Geoposition>();
            foreach (var point in Kinder(container, pointName))
            {
                ct.ThrowIfCancellationRequested();
                if (Position(point) is Geoposition position) points.Add(position);
            }
            if (points.Count > 0) sections.Add(points);
        }
        return sections;
    }

    /// <summary>Die direkten Kindelemente mit dem angegebenen Namen, gleich in welchem Namensraum.</summary>
    private static IEnumerable<XElement> Kinder(XElement element, string name) =>
        element.Elements().Where(kind => kind.Name.LocalName == name);

    /// <summary>Die Position eines Punkts aus den Attributen <c>lat</c> und <c>lon</c>; <c>null</c>, wenn ungültig.</summary>
    private static Geoposition? Position(XElement punkt) =>
        Zahl(punkt.Attribute("lat")) is double breite && breite is >= -90 and <= 90
        && Zahl(punkt.Attribute("lon")) is double laenge && laenge is >= -180 and <= 180
            ? new Geoposition(breite, laenge)
            : null;

    /// <summary>Eine Zahl mit Punkt als Dezimaltrennzeichen; <c>null</c>, wenn das Attribut fehlt oder keine Zahl ist.</summary>
    private static double? Zahl(XAttribute? attribut) =>
        attribut is not null && double.TryParse(attribut.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double wert) && double.IsFinite(wert)
            ? wert
            : null;
}
