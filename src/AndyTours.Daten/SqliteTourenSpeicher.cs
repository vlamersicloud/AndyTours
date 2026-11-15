using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;
namespace AndyTours.Daten;
/// <summary>SQLite-basierter Tourenspeicher mit ausschließlich serverseitig festgelegtem Datenbankpfad.</summary>
public sealed class SqliteTourenSpeicher(string datenbankpfad):ITourenSpeicher
{
 private SqliteConnection Oeffnen()=>OeffneDatenbank.Ausfuehren(datenbankpfad);
 public Task<TourListeAntwort> Liste(int start,int anzahl,DateOnly? von,DateOnly? bis,CancellationToken ct=default){ct.ThrowIfCancellationRequested();Validiere(start,anzahl,von,bis);using var c=Oeffnen();var all=TrainingseinheitenTabelle.Laden(c).Where(x=>(von==null||x.Datum>=von)&&(bis==null||x.Datum<=bis)).ToList();return Task.FromResult(new TourListeAntwort(all.Skip(start).Take(anzahl).Select(TourenMapper.ToAntwort).ToArray(),all.Count,start,anzahl));}
 public Task<TourAntwort?> Lesen(long id,CancellationToken ct=default){ct.ThrowIfCancellationRequested();using var c=Oeffnen();return Task.FromResult(TrainingseinheitenTabelle.Laden(c).Where(x=>x.Id==id).Select(TourenMapper.ToAntwort).FirstOrDefault());}
 public Task<TourAntwort> Anlegen(TourWerte werte,CancellationToken ct=default){ct.ThrowIfCancellationRequested();Validiere(werte);using var c=Oeffnen();var m=TourenMapper.ToModell(werte);var r=TrainingseinheitenTabelle.Einfuegen(c,m);m.Id=r.Id;return Task.FromResult(TourenMapper.ToAntwort(m));}
 public Task<TourAntwort> Aendern(long id,TourAendernAnfrage anfrage,CancellationToken ct=default){ct.ThrowIfCancellationRequested();if(anfrage is null)throw new UngueltigeTourException("Anfrage fehlt.");Validiere(anfrage.Werte);using var c=Oeffnen();using var tx=c.BeginTransaction(deferred:false);var m=TrainingseinheitenTabelle.Laden(c).FirstOrDefault(x=>x.Id==id);if(m==null)throw new TourNichtGefundenException(id);if(TourenMapper.ToAntwort(m).Version!=anfrage.Version)throw new TourVersionskonfliktException();var neu=TourenMapper.ToModell(anfrage.Werte,id);TrainingseinheitenTabelle.AendereInTransaktion(c,tx,neu);tx.Commit();return Task.FromResult(TourenMapper.ToAntwort(neu));}
 public Task Loeschen(long id,string version,CancellationToken ct=default){ct.ThrowIfCancellationRequested();using var c=Oeffnen();using var tx=c.BeginTransaction(deferred:false);var m=TrainingseinheitenTabelle.Laden(c).FirstOrDefault(x=>x.Id==id);if(m==null)throw new TourNichtGefundenException(id);if(TourenMapper.ToAntwort(m).Version!=version)throw new TourVersionskonfliktException();using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText="DELETE FROM Trainingseinheit WHERE Id=";cmd.Parameters.AddWithValue("",id);cmd.ExecuteNonQuery();tx.Commit();return Task.CompletedTask;}
 private static void Validiere(TourWerte w)=>ValidiereTour.Ausfuehren(w);
 private static void Validiere(int s,int n,DateOnly? v,DateOnly? b)=>ValidiereTour.Liste(s,n,v,b);
}
