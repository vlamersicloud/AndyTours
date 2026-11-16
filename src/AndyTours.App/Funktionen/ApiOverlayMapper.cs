using System.Collections.ObjectModel;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using CoreFarbe = AndyTours.Core.Modell.Overlayfarbe;
using ApiFarbe = AndyTours.Contracts.V1.OverlayFarbe;

namespace AndyTours.App.Funktionen;

/// <summary>Pure mapping of one version-matched API overlay into the existing presentation model.</summary>
public static class ApiOverlayMapper
{
    public static Overlay AlsOverlay(OverlayMetadatenAntwort meta, OverlayInhaltAntwort inhalt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (meta is null || inhalt is null || meta.Id <= 0 || inhalt.Id != meta.Id ||
            string.IsNullOrEmpty(meta.Version) || !string.Equals(meta.Version, inhalt.Version, StringComparison.Ordinal) ||
            meta.Name is null || inhalt.Abschnitte is null || inhalt.Abschnitte.Count == 0 ||
            !Enum.IsDefined(meta.Farbe ?? ApiFarbe.Violett))
            throw Invalid();

        var sections = new List<IReadOnlyList<Geoposition>>(inhalt.Abschnitte.Count);
        foreach (var section in inhalt.Abschnitte)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (section is null || section.Count == 0) throw Invalid();
            var points = new List<Geoposition>(section.Count);
            foreach (var point in section)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (point is null || !double.IsFinite(point.Breite) || point.Breite is < -90 or > 90 ||
                    !double.IsFinite(point.Laenge) || point.Laenge is < -180 or > 180)
                    throw Invalid();
                points.Add(new Geoposition(point.Breite, point.Laenge));
            }
            sections.Add(new ReadOnlyCollection<Geoposition>(points));
        }

        return new Overlay(meta.Id, meta.OrdnerId, meta.Name, meta.Farbe is { } color ? Farbe(color) : null,
            meta.IstEingeschaltet, new ReadOnlyCollection<IReadOnlyList<Geoposition>>(sections));
    }

    public static CoreFarbe Farbe(ApiFarbe farbe) => farbe switch
    {
        ApiFarbe.Violett => CoreFarbe.Violett,
        ApiFarbe.Blau => CoreFarbe.Blau,
        ApiFarbe.Hellblau => CoreFarbe.Hellblau,
        ApiFarbe.Tuerkis => CoreFarbe.Tuerkis,
        ApiFarbe.Gruen => CoreFarbe.Gruen,
        ApiFarbe.Gelb => CoreFarbe.Gelb,
        ApiFarbe.Orange => CoreFarbe.Orange,
        ApiFarbe.Rot => CoreFarbe.Rot,
        ApiFarbe.Pink => CoreFarbe.Pink,
        ApiFarbe.Braun => CoreFarbe.Braun,
        ApiFarbe.Grau => CoreFarbe.Grau,
        ApiFarbe.Schwarz => CoreFarbe.Schwarz,
        _ => throw Invalid()
    };

    private static InvalidOperationException Invalid() => new("Overlay-Antwort ungültig.");
}
