using AndyTours.Application.Stammdaten;
using AndyTours.Application.Tabellenlayout;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Tabellenlayout;

public sealed class EinheitentabellenlayoutRegelnTests
{
    private static EinheitentabellenlayoutAnfrage Valid() => new(new string('A', 64),
        Enum.GetValues<Einheitentabellenschluessel>().Select(x => new Einheitentabellenspalte(x, null)).ToArray(), []);

    private static void Invalid(EinheitentabellenlayoutAnfrage request)
    {
        var ex = Assert.Throws<StammdatenException>(() => EinheitentabellenlayoutRegeln.Pruefe(request));
        Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
    }

    [Fact]
    public void AcceptsCompleteLayoutAndAutoWidths() => EinheitentabellenlayoutRegeln.Pruefe(Valid());

    [Fact]
    public void RejectsIndependentInvalidLayoutCases()
    {
        var v = Valid();
        Invalid(v with { Anordnung = v.Anordnung.Select((x, i) => i == 0 ? null! : x).ToArray() });
        Invalid(v with { Anordnung = v.Anordnung.Select((x, i) => i == 0 ? v.Anordnung[1] : x).ToArray() });
        Invalid(v with { Anordnung = v.Anordnung.Select((x, i) => i == 0 ? x with { Schluessel = (Einheitentabellenschluessel)99 } : x).ToArray() });
        Invalid(v with { Anordnung = v.Anordnung.Skip(1).ToArray() });
        Invalid(v with { Version = "bad" });
        Invalid(v with { Anordnung = v.Anordnung.Select((x, i) => i == 0 ? x with { Breite = double.NaN } : x).ToArray() });
        Invalid(v with { Anordnung = v.Anordnung.Select((x, i) => i == 0 ? x with { Breite = double.PositiveInfinity } : x).ToArray() });
        Invalid(v with { Anordnung = v.Anordnung.Select((x, i) => i == 0 ? x with { Breite = -1 } : x).ToArray() });
        Invalid(v with { Anordnung = v.Anordnung.Select((x, i) => i == 0 ? x with { Breite = 0.01 } : x).ToArray() });
        EinheitentabellenlayoutRegeln.Pruefe(v with { Anordnung = v.Anordnung.Select(x => x with { Breite = null }).ToArray() });
        Invalid(v with { Ausgeblendet = [Einheitentabellenschluessel.Datum, Einheitentabellenschluessel.Datum] });
        Invalid(v with { Ausgeblendet = [(Einheitentabellenschluessel)99] });
        Invalid(v with { Ausgeblendet = Enum.GetValues<Einheitentabellenschluessel>() });
        EinheitentabellenlayoutRegeln.Pruefe(v with { Ausgeblendet = Enum.GetValues<Einheitentabellenschluessel>().Skip(1).ToArray() });
    }
}
