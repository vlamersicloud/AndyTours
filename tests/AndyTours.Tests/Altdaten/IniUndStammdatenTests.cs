using AndyTours.Core.Modell;
using AndyTours.Import.Altdaten;

namespace AndyTours.Tests.Altdaten;

/// <summary>
/// Tests für das Lesen der INI-Datei und das Umwandeln in Stammdaten.
/// </summary>
public class IniUndStammdatenTests
{
    /// <summary>Verkleinerter Auszug aus der echten bikexperience.ini.</summary>
    private static readonly string[] IniZeilen =
    {
        "[Activity]",
        "Bike=true;km/h;8000;80000;100",
        "Laufen=false;min/km;1000;11000;100",
        "",
        "[FieldLengths]",
        "fieldLenSUM=75",
        "[Category]",
        "Arbeit=false",
        "Trainieren=true",
        "[Biker]",
        "firstname=Andy",
        "birthday=28.05.1963",
        "pulsemin=50",
        "pulsemax=168",
        "[Equipment.Genius/Bike]",
        "activity=Bike",
        "date=16.7.2018",
        "rejected=false",
        "[FieldLengths]",
        "fieldLenHM=50",
    };

    [Fact]
    public void Doppelte_Abschnitte_werden_zusammengefuehrt()
    {
        IniDatei ini = LeseIniDatei.Ausfuehren(IniZeilen);

        Assert.Equal(2, ini.Abschnitte["FieldLengths"].Count);
    }

    [Fact]
    public void Aktivitaeten_werden_gelesen()
    {
        List<Aktivitaet> aktivitaeten = WandleAktivitaeten.Ausfuehren(LeseIniDatei.Ausfuehren(IniZeilen));

        Assert.Equal(2, aktivitaeten.Count);
        Assert.True(aktivitaeten[0].IstHaupt);
        Assert.Equal(8000, aktivitaeten[0].ZielKm);
        Assert.Equal("min/km", aktivitaeten[1].Geschwindigkeitseinheit);
    }

    [Fact]
    public void Fahrer_wird_gelesen()
    {
        Fahrer fahrer = WandleFahrer.Ausfuehren(LeseIniDatei.Ausfuehren(IniZeilen));

        Assert.Equal("Andy", fahrer.Vorname);
        Assert.Equal(new DateOnly(1963, 5, 28), fahrer.Geburtstag);
        Assert.Equal(50, fahrer.Ruhepuls);
        Assert.Equal(168, fahrer.Maxpuls);
    }

    [Fact]
    public void Equipment_wird_mit_Aktivitaet_verknuepft()
    {
        IniDatei ini = LeseIniDatei.Ausfuehren(IniZeilen);
        List<Equipment> equipments = WandleEquipments.Ausfuehren(ini, WandleAktivitaeten.Ausfuehren(ini));

        Equipment genius = Assert.Single(equipments);
        Assert.Equal("Genius/Bike", genius.Name);
        Assert.Equal(1, genius.AktivitaetId);
        Assert.Equal(new DateOnly(2018, 7, 16), genius.Anschaffungsdatum);
    }

    [Fact]
    public void Equipment_wird_auch_ueber_Kurznamen_gefunden()
    {
        var equipments = new List<Equipment>
        {
            new() { Id = 1, Name = "Genius/Bike" },
            new() { Id = 2, Name = "FOCUS Raven 8.9/Mountainbiken" },
            new() { Id = 3, Name = "FELT" },
        };

        Assert.Equal(1, FindeEquipmentZuName.Ausfuehren(equipments, "Genius")?.Id);
        Assert.Equal(2, FindeEquipmentZuName.Ausfuehren(equipments, "FOCUS Raven 8.9")?.Id);
        Assert.Equal(3, FindeEquipmentZuName.Ausfuehren(equipments, "felt")?.Id);
        Assert.Null(FindeEquipmentZuName.Ausfuehren(equipments, "MOUNTAIN"));
    }
}
