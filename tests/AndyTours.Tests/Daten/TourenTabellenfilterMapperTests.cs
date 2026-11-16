using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;

namespace AndyTours.Tests.Daten;

public sealed class TourenTabellenfilterMapperTests
{
    [Fact]
    public void Null_maps_to_the_shared_empty_filter()
    {
        Assert.Equal(Einheitenfilter.Leer, TourenTabellenfilterMapper.ToFilter(null));
    }

    [Fact]
    public void Maps_exact_text_without_category_or_source_mutation()
    {
        var raw = new TourenTabellenfilter("Bike (ausgemustert)", "GA1", "1,5", "20.25", "0", "800", "two words");
        var before = raw with { };
        var result = TourenTabellenfilterMapper.ToFilter(raw);
        Assert.Null(result.Kategorie);
        Assert.Equal("Bike (ausgemustert)", result.Equipment);
        Assert.Equal("GA1", result.Trainingsbereich);
        Assert.Equal("two words", result.Suchbegriff);
        Assert.Equal(before, raw);
        Assert.Equal(new Filterbereich(1.5, 20.25), result.Distanz);
        Assert.Equal(new Filterbereich(0, 800), result.Hoehenmeter);
    }

    [Fact]
    public void Blank_raw_strings_are_preserved_except_null_search_uses_empty_string()
    {
        var result = TourenTabellenfilterMapper.ToFilter(new TourenTabellenfilter(
            EquipmentName: "", Trainingsbereich: null, DistanzVon: " ", DistanzBis: null,
            HoehenmeterVon: null, HoehenmeterBis: "", Suchbegriff: null));
        Assert.Equal("", result.Equipment);
        Assert.Null(result.Trainingsbereich);
        Assert.Equal("", result.Suchbegriff);
        Assert.Equal(Filterbereich.Leer, result.Distanz);
        Assert.Equal(Filterbereich.Leer, result.Hoehenmeter);
    }

    [Theory]
    [InlineData("bad", null, true, false)]
    [InlineData("20", "10", true, true)]
    [InlineData("NaN", null, true, false)]
    public void Invalid_and_inverted_ranges_are_inactive_with_core_error_flags(
        string? from, string? to, bool fromInvalid, bool toInvalid)
    {
        var result = TourenTabellenfilterMapper.ToFilter(new TourenTabellenfilter(
            DistanzVon: from, DistanzBis: to));
        Assert.False(result.Distanz.IstAktiv);
        Assert.Equal(fromInvalid, result.Distanz.VonFehlerhaft);
        Assert.Equal(toInvalid, result.Distanz.BisFehlerhaft);
    }
}