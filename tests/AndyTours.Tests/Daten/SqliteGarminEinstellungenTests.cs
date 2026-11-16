using System.Text.Json;
using AndyTours.Application.Garmin;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;
namespace AndyTours.Tests.Daten;
public sealed class SqliteGarminEinstellungenTests : IDisposable
{
    private readonly string _dir=Path.Combine(Path.GetTempPath(),"AndyTours-GarminTest-"+Guid.NewGuid().ToString("N"));
    private string Db=>Path.Combine(_dir,"test.db");
    private SqliteGarminEinstellungen Service=>new(Db);
    private static readonly string[] ExpectedStatusProperties = ["AllePaketeAutomatisch", "HatSchluessel", "Version"];
    public SqliteGarminEinstellungenTests(){Directory.CreateDirectory(_dir); using var db=OeffneDatenbank.Ausfuehren(Db);}
    [Fact] public async Task LadenLeerIstSchreibfreiUndKeyBleibtGeheim()
    {
        var empty=await Service.Laden(); Assert.False(empty.HatSchluessel); Assert.False(empty.AllePaketeAutomatisch);
        using(var db=OeffneDatenbank.Ausfuehren(Db)) Assert.Null(EinstellungTabelle.Lesen(db,Einstellungsschluessel.GarminPersonalApiKey));
        const string fake="FakeKey_Only+123"; var saved=await Service.SchluesselSpeichern(new(empty.Version,fake));
        var json=JsonSerializer.Serialize(saved); Assert.DoesNotContain(fake,json); Assert.True(saved.HatSchluessel);
        using(var status=JsonDocument.Parse(json)) Assert.Equal(ExpectedStatusProperties,status.RootElement.EnumerateObject().Select(p=>p.Name).OrderBy(x=>x,StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(fake,new GarminSchluesselAnfrage(saved.Version,fake).ToString());
    }
    [Fact] public async Task AutomatikUndEntfernenLassenJeweilsAndereEinstellungUnveraendert()
    {
        const string fake="Fake-123"; var initial=await Service.Laden(); var saved=await Service.SchluesselSpeichern(new(initial.Version,fake));
        var auto=await Service.AutomatikSpeichern(new(saved.Version,true));
        using(var db=OeffneDatenbank.Ausfuehren(Db)) Assert.Equal(fake,GarminEinstellung.Laden(db));
        var removed=await Service.SchluesselEntfernen(new(auto.Version)); Assert.True(removed.AllePaketeAutomatisch); Assert.False(removed.HatSchluessel);
        var empty=await Service.SchluesselSpeichern(new(removed.Version,"")); Assert.True(empty.AllePaketeAutomatisch);
        using(var db=OeffneDatenbank.Ausfuehren(Db)) Assert.Null(GarminEinstellung.Laden(db));
    }
    [Fact] public async Task LegacyKeyReplacementInvalidatesCasWithoutOverwrite()
    {
        var first=await Service.Laden(); var saved=await Service.SchluesselSpeichern(new(first.Version,"Fake-old"));
        using(var db=OeffneDatenbank.Ausfuehren(Db)) GarminEinstellung.Speichern(db,"Fake-new");
        var error=await Assert.ThrowsAsync<StammdatenException>(()=>Service.AutomatikSpeichern(new(saved.Version,true)));
        Assert.Equal(StammdatenFehlerart.Konflikt,error.Art); Assert.DoesNotContain("Fake",error.Message);
        using(var db=OeffneDatenbank.Ausfuehren(Db)) Assert.Equal("Fake-new",GarminEinstellung.Laden(db));
    }
    [Fact] public async Task UngueltigeSchluesselUndVersionenSindSicher()
    {
        var state=await Service.Laden();
        foreach(var key in new string?[]{null,"Fake key","Fake!","x".PadLeft(4097,'x')})
        {
            var error=await Assert.ThrowsAsync<StammdatenException>(()=>Service.SchluesselSpeichern(new(state.Version,key!)));
            Assert.Equal(StammdatenFehlerart.Ungueltig,error.Art); Assert.DoesNotContain("Fake",error.Message);
        }
        var invalid=await Assert.ThrowsAsync<StammdatenException>(()=>Service.AutomatikSpeichern(new("invalid",true)));
        Assert.Equal(StammdatenFehlerart.Ungueltig,invalid.Art);
    }
    [Fact] public async Task LeererKeyEntferntKeyUndBewahrtAutomatik()
    {
        var initial=await Service.Laden(); var keyed=await Service.SchluesselSpeichern(new(initial.Version,"Fake-empty"));
        var automatic=await Service.AutomatikSpeichern(new(keyed.Version,true));
        var empty=await Service.SchluesselSpeichern(new(automatic.Version,""));
        Assert.False(empty.HatSchluessel); Assert.True(empty.AllePaketeAutomatisch);
        using var db=OeffneDatenbank.Ausfuehren(Db); Assert.Null(GarminEinstellung.Laden(db)); Assert.True(GarminEinstellung.AutomatischLaden(db));
    }
    [Fact] public async Task AbbruchLaesstEinstellungenUnveraendert()
    {
        var state=await Service.Laden(); using var cts=new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>Service.SchluesselSpeichern(new(state.Version,"Fake-cancel"),cts.Token));
        using var db=OeffneDatenbank.Ausfuehren(Db); Assert.Null(GarminEinstellung.Laden(db)); Assert.False(GarminEinstellung.AutomatischLaden(db));
    }
    public void Dispose(){SqliteConnection.ClearAllPools();if(Directory.Exists(_dir))Directory.Delete(_dir,true);}
}
