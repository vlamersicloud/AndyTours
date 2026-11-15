using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;

namespace AndyTours.Tests.Berechnungen;

/// <summary>Zeitgewichtete bpm-Bereiche, klare Grenzen und keine erfundenen Zeiten bei fehlenden Daten.</summary>
public sealed class PulszeitenTests
{
    private static readonly GarminPulsprofil Profil = new("CYCLING", new[] { 90, 110, 130, 150, 170 });

    [Fact]
    public void GarminGrenzenSindNichtUeberlappend()
    {
        Assert.Equal(0, Profil.Zone(89));
        Assert.Equal(1, Profil.Zone(90));
        Assert.Equal(1, Profil.Zone(109));
        Assert.Equal(2, Profil.Zone(110));
        Assert.Equal(5, Profil.Zone(170));
        Assert.Equal("90–109 bpm", Profil.Bereich(1));
        Assert.Equal("ab 170 bpm", Profil.Bereich(5));
    }

    [Fact]
    public void DauerWirdNachZeitUndNichtNachAnzahlMesspunkteGewichtet()
    {
        var zeiten = BerechnePulszeiten.Ausfuehren(new[]
        {
            Punkt(0, 0, 100), Punkt(10, 10, 140), Punkt(40, 40, 160),
        }, Profil);
        Assert.Equal(10, zeiten[1]);
        Assert.Equal(30, zeiten[3]);
        Assert.Equal(40, zeiten.Values.Sum()); // keine Zeit hinter dem letzten Punkt
    }

    [Fact]
    public void PauseZaehltNichtUndFehlwerteWerdenNichtInterpoliert()
    {
        var zeiten = BerechnePulszeiten.Ausfuehren(new[]
        {
            Punkt(0, 0, 100), Punkt(20, 0, 100), Punkt(40, 20, 100),
            Punkt(50, 30, null), Punkt(60, 40, 140),
        }, Profil);
        Assert.Equal(20, zeiten.Values.Sum());
    }

    [Fact]
    public void GrosseMesslueckeZaehltTrotzKurzerAktiverZeitNicht()
    {
        var zeiten = BerechnePulszeiten.Ausfuehren(new[] { Punkt(0, 0, 120), Punkt(300, 10, 120) }, Profil);
        Assert.Empty(zeiten);
    }

    [Fact]
    public void OhneGarminProfilWerdenNurGemesseneZehnerbereicheErmittelt()
    {
        var zeiten = BerechnePulszeiten.Ausfuehren(new[] { Punkt(0, 0, 123), Punkt(10, 10, 129) }, null);
        Assert.Equal(10, zeiten[120]);
    }

    private static Verlaufspunkt Punkt(int echt, int aktiv, int? puls) => new()
    {
        ZeitpunktUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(echt),
        Fahrzeit = TimeSpan.FromSeconds(aktiv), Puls = puls,
    };
}
