namespace AndyTours.Tests.Sicherung;

using System.Text.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Xunit;

public sealed class WiederherstellungsvorschauRegelnTests
{
    private static readonly Guid ErwarteteId = Guid.Parse("4e1f6d1c-1284-4a7e-a95b-2e7b6215ed10");
    private static readonly string HashGross = new('A', 64);
    private static readonly string HashKlein = new('a', 64);
    private const string Fehlertext = "Sicherungsanforderung für die Vorschau ist ungültig.";

    [Fact]
    public void GueltigerGemischterHashWirdNormalisiertOhneOriginalZuAendern()
    {
        var original = new WiederherstellungsvorschauAnfrage(ErwarteteId, HashKlein);
        var geprueft = WiederherstellungsvorschauRegeln.PruefeAnfrage(original);

        Assert.Equal(ErwarteteId, geprueft.SicherungsId);
        Assert.Equal(HashGross, geprueft.ErwarteterInhaltHash);
        Assert.Equal(HashKlein, original.ErwarteterInhaltHash);
    }

    [Fact]
    public void NumerischerUndGemischtGrossKleinerHexHashSindGueltig()
    {
        Assert.Equal(new string('0', 64), WiederherstellungsvorschauRegeln.PruefeAnfrage(
            new WiederherstellungsvorschauAnfrage(ErwarteteId, new string('0', 64))).ErwarteterInhaltHash);
        const string mixed = "0123456789abcdefABCDEF0123456789abcdefABCDEF0123456789abcdefABCD";
        Assert.Equal(mixed.ToUpperInvariant(), WiederherstellungsvorschauRegeln.PruefeAnfrage(
            new WiederherstellungsvorschauAnfrage(ErwarteteId, mixed)).ErwarteterInhaltHash);
    }

    [Theory]
    [MemberData(nameof(UngueltigeAnfragen))]
    public void UngueltigeAnfragenWerdenSicherAbgelehnt(WiederherstellungsvorschauAnfrage? anfrage)
    {
        var fehler = Assert.Throws<StammdatenException>(() =>
            WiederherstellungsvorschauRegeln.PruefeAnfrage(anfrage!));
        Assert.Equal(StammdatenFehlerart.Ungueltig, fehler.Art);
        Assert.Equal(Fehlertext, fehler.Message);
        Assert.DoesNotContain("PRIVATE", fehler.Message);
    }

    public static IEnumerable<object?[]> UngueltigeAnfragen()
    {
        yield return [null];
        yield return [new WiederherstellungsvorschauAnfrage(Guid.Empty, HashGross)];
        yield return [new WiederherstellungsvorschauAnfrage(ErwarteteId, null!)];
        yield return [new WiederherstellungsvorschauAnfrage(ErwarteteId, "")];
        yield return [new WiederherstellungsvorschauAnfrage(ErwarteteId, new string('a', 63))];
        yield return [new WiederherstellungsvorschauAnfrage(ErwarteteId, new string('a', 65))];
        yield return [new WiederherstellungsvorschauAnfrage(ErwarteteId, "PRIVATE" + new string('a', 57))];
    }

    [Fact]
    public void AntwortBewahrtUnbekannteWerteAlsNullUndFreigabeAlsFalse()
    {
        var antwort = new WiederherstellungsvorschauAntwort(
            new SicherungAntwort(ErwarteteId, Sicherungsart.Vollarchiv, new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero), 1, HashGross),
            null, null, null, false, false, Array.Empty<string>());
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            RespectRequiredConstructorParameters = true
        };
        var json = JsonSerializer.Serialize(antwort, serializerOptions);
        var roundtrip = JsonSerializer.Deserialize<WiederherstellungsvorschauAntwort>(json, serializerOptions)!;

        Assert.Null(roundtrip.ManifestFormatversion);
        Assert.Null(roundtrip.Dateianzahl);
        Assert.Null(roundtrip.NutzdatenBytes);
        Assert.False(roundtrip.DatenbankGeprueft);
        Assert.False(roundtrip.AnwendungFreigegeben);
        Assert.Contains("\"anwendungFreigegeben\":false", json);
    }
}
