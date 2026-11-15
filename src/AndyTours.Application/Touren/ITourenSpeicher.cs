using AndyTours.Contracts.V1;
namespace AndyTours.Application.Touren;
/// <summary>Persistenz-Port für einzeln versionierte Tourenressourcen.</summary>
public interface ITourenSpeicher
{
 Task<TourListeAntwort> Liste(int start,int anzahl,DateOnly? von,DateOnly? bis,CancellationToken ct=default);
 Task<TourAntwort?> Lesen(long id,CancellationToken ct=default);
 Task<TourAntwort> Anlegen(TourWerte werte,CancellationToken ct=default);
 /// <summary>Legt eine Tour mit einer stabilen Idempotenzkennung an.</summary>
 Task<TourAntwort> AnlegenIdempotent(TourWerte werte,Guid anfrageId,CancellationToken ct=default);
 Task<TourAntwort> Aendern(long id,TourAendernAnfrage anfrage,CancellationToken ct=default);
 Task Loeschen(long id,string version,CancellationToken ct=default);
}
