using AndyTours.Core.Berechnungen;

namespace AndyTours.Tests.Berechnungen;

/// <summary>
/// Tests für <see cref="VergleicheNamen"/>, <see cref="ErmittleNamensliste"/> und <see cref="ZaehleNamen"/>.
/// </summary>
public class NamenTests
{
    [Theory]
    [InlineData("Kette", "Kette", true)]
    [InlineData(" kette ", "KETTE", true)]
    [InlineData("Kette", "Ketten", false)]
    [InlineData("", " ", true)]
    public void Namen_gleichen_sich_ohne_Unterschied_der_Schreibweise_und_Randleerzeichen(string a, string b, bool erwartet)
    {
        Assert.Equal(erwartet, VergleicheNamen.Ausfuehren(a, b));
    }

    [Fact]
    public void Namensliste_alphabetisch_ohne_leere_und_doppelte()
    {
        List<string> liste = ErmittleNamensliste.Ausfuehren(["WSA", "GA2", "", "GA1", "GA2", " K0 ", "SK"]);

        Assert.Equal(["GA1", "GA2", "K0", "SK", "WSA"], liste);
    }

    [Fact]
    public void Schreibvarianten_ergeben_einen_Eintrag_in_der_zuerst_gefundenen_Schreibweise()
    {
        List<string> liste = ErmittleNamensliste.Ausfuehren(["GA1", "ga1", "Ga1"]);

        Assert.Equal(["GA1"], liste);
    }

    [Fact]
    public void Ohne_Namen_ist_die_Liste_leer()
    {
        Assert.Empty(ErmittleNamensliste.Ausfuehren([]));
    }

    [Theory]
    [InlineData("Kettenwechsel", 2)]
    [InlineData("KETTENWECHSEL", 2)]
    [InlineData("dämpfer eingestellt", 1)]
    [InlineData("Inspektion", 0)]
    public void Zaehlt_ohne_Unterschied_der_Schreibweise(string gesucht, int erwartet)
    {
        string[] namen = ["Kettenwechsel", "Dämpfer eingestellt", "kettenwechsel ", " "];

        Assert.Equal(erwartet, ZaehleNamen.Ausfuehren(namen, gesucht));
    }
}
