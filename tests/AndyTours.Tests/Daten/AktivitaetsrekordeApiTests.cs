using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;
namespace AndyTours.Tests.Daten;
public sealed class AktivitaetsrekordeApiTests : IDisposable
{
    private readonly string _dir=Path.Combine(Path.GetTempPath(),"AndyTours-Rekorde-"+Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task RekordeBleibenImZeitraumUndZukunftshinweisBleibtGesamtbestand()
    {
        Directory.CreateDirectory(_dir); var path=Path.Combine(_dir,"test.db"); long activity, historicalId, futureId;
        using(var db=OeffneDatenbank.Ausfuehren(path))
        {
            using var tx=db.BeginTransaction();
            activity=AktivitaetenTabelle.Einfuegen(db,tx,new Aktivitaet{Name="Rad"}); tx.Commit();
            historicalId=TrainingseinheitenTabelle.Einfuegen(db,new Trainingseinheit{AktivitaetId=activity,Datum=new DateOnly(2026,5,1),Name="Mai",DistanzKm=10,Fahrzeit=TimeSpan.FromHours(1)}).Id;
            futureId=TrainingseinheitenTabelle.Einfuegen(db,new Trainingseinheit{AktivitaetId=activity,Datum=new DateOnly(2046,3,31),Name="Fern",DistanzKm=999,Fahrzeit=TimeSpan.FromHours(1)}).Id;
        }
        try
        {
            var result=await new SqliteAktivitaetsauswertung(path).Laden(new(new DateOnly(2026,1,1),new DateOnly(2026,12,31),Auswertungsraster.Monat,activity,null));
            Assert.Equal(1,result.ZukuenftigeAktivitaeten);
            var record=Assert.Single(result.Einzelrekorde,x=>x.Bezeichnung=="Längste Tour");
            Assert.Equal(historicalId,record.TourId);
            Assert.DoesNotContain(result.Jahresrekorde.Concat(result.Einzelrekorde),x=>x.Herkunft.Contains("2046",StringComparison.Ordinal));
            var annual=Assert.Single(result.Jahresrekorde,x=>x.Bezeichnung=="Kilometer");
            Assert.Equal("10,0 km",annual.Wert); Assert.Equal("2026",annual.Herkunft);
            Assert.NotEqual(futureId,record.TourId);
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(_dir,true); }
    }
    public void Dispose(){ SqliteConnection.ClearAllPools(); if(Directory.Exists(_dir)) Directory.Delete(_dir,true); }
}
