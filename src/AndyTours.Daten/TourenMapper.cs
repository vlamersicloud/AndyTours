using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
namespace AndyTours.Daten;
/// <summary>Übersetzt Touren zwischen stabilen API-Verträgen und dem internen Fachmodell.</summary>
public static class TourenMapper
{
 /// <summary>Erzeugt eine Antwort mit SHA-256-Version über ID und kanonische Werte.</summary>
 public static TourAntwort ToAntwort(Trainingseinheit t)
 {
  var w=new TourWerte(t.FahrerId,t.Datum,t.Startzeit,t.Name,checked((long)t.Fahrzeit.TotalSeconds),t.DistanzKm,t.Hoehenmeter,t.MaxHoehe,t.StartHoehe,t.MaxGeschwindigkeit,t.HfDurchschnitt,t.HfMax,t.HfMin,t.Trittfrequenz,t.Temperatur,t.LeistungWatt,t.KoerpergewichtKg,t.Erholungspuls,t.Trainingsbereich,t.Kommentar,t.AktivitaetId,t.EquipmentId,t.KategorieIds.Order().ToArray());
  var bytes=JsonSerializer.SerializeToUtf8Bytes(new{t.Id,Werte=w});
  return new(t.Id,Convert.ToHexString(SHA256.HashData(bytes)),w);
 }
 /// <summary>Erzeugt das Fachmodell ohne Anhänge aus API-Werten.</summary>
 public static Trainingseinheit ToModell(TourWerte w,long id=0)=>new(){Id=id,FahrerId=w.FahrerId,Datum=w.Datum,Startzeit=w.Startzeit,Name=w.Name,Fahrzeit=TimeSpan.FromSeconds(w.FahrzeitSekunden),DistanzKm=w.DistanzKm,Hoehenmeter=w.Hoehenmeter,MaxHoehe=w.MaxHoehe,StartHoehe=w.StartHoehe,MaxGeschwindigkeit=w.MaxGeschwindigkeit,HfDurchschnitt=w.HfDurchschnitt,HfMax=w.HfMax,HfMin=w.HfMin,Trittfrequenz=w.Trittfrequenz,Temperatur=w.Temperatur,LeistungWatt=w.LeistungWatt,KoerpergewichtKg=w.KoerpergewichtKg,Erholungspuls=w.Erholungspuls,Trainingsbereich=w.Trainingsbereich,Kommentar=w.Kommentar,AktivitaetId=w.AktivitaetId,EquipmentId=w.EquipmentId,KategorieIds=w.KategorieIds.ToList()};
}
