using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;

namespace AndyTours.Tests.Berechnungen;

/// <summary>
/// Tests für <see cref="BenenneTaetigkeitUm"/>.
/// </summary>
public class BenenneTaetigkeitUmTests
{
    /// <summary>
    /// Wartungen mit „Kettenwechsel“ (zwei Schreibweisen), „Dämpfer eingestellt“ und ohne Tätigkeit, dazu ein Alarm
    /// „Kettenwechsel“.
    /// </summary>
    private static List<Wartung> Wartungen() =>
    [
        new() { Id = 1, Taetigkeit = "Kettenwechsel" },
        new() { Id = 2, Taetigkeit = "Dämpfer eingestellt" },
        new() { Id = 3, Taetigkeit = "kettenwechsel " },
        new() { Id = 4, Taetigkeit = " " },
        new() { Id = 5, Taetigkeit = "Bremsbelag HR" },
        new() { Id = 6, Taetigkeit = "Kettenwechsel", Typ = WartungsTyp.Alarm, AlarmKm = 3000 },
    ];

    [Fact]
    public void Umbenennen_aendert_alle_Schreibweisen_auch_im_Alarm()
    {
        List<Wartung> wartungen = Wartungen();

        int anzahl = BenenneTaetigkeitUm.Ausfuehren(wartungen, "Kettenwechsel", " Kette gewechselt ");

        Assert.Equal(3, anzahl);
        Assert.Equal(["Kette gewechselt", "Dämpfer eingestellt", "Kette gewechselt", " ", "Bremsbelag HR", "Kette gewechselt"],
            wartungen.Select(w => w.Taetigkeit));
    }
}
