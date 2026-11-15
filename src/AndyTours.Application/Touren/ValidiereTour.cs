using AndyTours.Contracts.V1;
namespace AndyTours.Application.Touren;
/// <summary>Validiert alle eingehenden Tourwerte an der Vertrauensgrenze.</summary>
public static class ValidiereTour
{
 /// <summary>Prüft Vollständigkeit, Wertebereiche und Transportgrenzen.</summary>
 public static void Ausfuehren(TourWerte? w)
 {
  if(w is null) throw new UngueltigeTourException("Tourwerte fehlen.");
  if(w.FahrerId<0||w.AktivitaetId<=0||w.EquipmentId is <=0) Ungueltig("Kennungen müssen positiv sein.");
  if(w.FahrzeitSekunden<0||w.FahrzeitSekunden>TimeSpan.MaxValue.Ticks/TimeSpan.TicksPerSecond) Ungueltig("Fahrzeit ist ungültig.");
  if(!double.IsFinite(w.DistanzKm)||w.DistanzKm<0||!double.IsFinite(w.LeistungWatt)||w.LeistungWatt<0||!double.IsFinite(w.KoerpergewichtKg)||w.KoerpergewichtKg<0||!double.IsFinite(w.MaxGeschwindigkeit)||w.MaxGeschwindigkeit<0||w.Hoehenmeter<0) Ungueltig("Messwerte müssen endlich und nicht negativ sein.");
  if(w.Name is null||w.Name.Length>500||w.Kommentar is null||w.Kommentar.Length>10000||w.Trainingsbereich is null||w.Trainingsbereich.Length>200) Ungueltig("Textfeld fehlt oder überschreitet die erlaubte Länge.");
  if(w.KategorieIds is null||w.KategorieIds.Length>200||w.KategorieIds.Any(x=>x<=0)||w.KategorieIds.Distinct().Count()!=w.KategorieIds.Length) Ungueltig("Kategorien sind ungültig.");
 }
 /// <summary>Prüft Paging und Zeitraum einer Liste.</summary>
 public static void Liste(int start,int anzahl,DateOnly? von,DateOnly? bis)
 { if(start<0||anzahl<1||anzahl>200||von>bis) Ungueltig("Paging oder Zeitraum ist ungültig."); }
 private static void Ungueltig(string meldung)=>throw new UngueltigeTourException(meldung);
}
