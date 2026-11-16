using Microsoft.Data.Sqlite;
namespace AndyTours.Daten;
internal static class GarminImportJobSchema
{
 internal static void Ensure(SqliteConnection connection, SqliteTransaction transaction)
 {
  using var command=connection.CreateCommand(); command.Transaction=transaction;
  command.CommandText="""
  CREATE TABLE IF NOT EXISTS GarminImportJob (JobId TEXT PRIMARY KEY, StartAnfrageId TEXT NOT NULL UNIQUE, Zustand INTEGER NOT NULL CHECK(Zustand BETWEEN 0 AND 5), Revision INTEGER NOT NULL CHECK(Revision>0), Paketnummer INTEGER NOT NULL CHECK(Paketnummer>=1), WeitereMoeglich INTEGER NOT NULL CHECK(WeitereMoeglich IN(0,1)), Automatisch INTEGER NOT NULL CHECK(Automatisch IN(0,1)), ErstelltUtc TEXT NOT NULL, AktualisiertUtc TEXT NOT NULL, WarncodesJson TEXT NOT NULL, LeaseOwner TEXT, LeaseBisUtc TEXT, LeaseGeneration INTEGER NOT NULL CHECK(LeaseGeneration>=0));
  CREATE UNIQUE INDEX IF NOT EXISTS UX_GarminImportJob_EinAktiverJob ON GarminImportJob ((1)) WHERE Zustand IN(0,1,2,3);
  CREATE TABLE IF NOT EXISTS GarminImportKandidat (KandidatId TEXT PRIMARY KEY, JobId TEXT NOT NULL REFERENCES GarminImportJob(JobId) ON DELETE CASCADE, GarminAktivitaetId INTEGER NOT NULL CHECK(GarminAktivitaetId>0), Paketnummer INTEGER NOT NULL CHECK(Paketnummer>=1), Zustand INTEGER NOT NULL CHECK(Zustand BETWEEN 0 AND 5), Datum TEXT, Name TEXT NOT NULL, DistanzKm REAL, FahrzeitSekunden INTEGER, Warncode TEXT, UNIQUE(JobId,GarminAktivitaetId));
  CREATE TABLE IF NOT EXISTS GarminImportSteuerung (JobId TEXT NOT NULL REFERENCES GarminImportJob(JobId) ON DELETE CASCADE, AnfrageId TEXT NOT NULL, Aktion TEXT NOT NULL, AnfrageHash TEXT NOT NULL, AntwortJson TEXT NOT NULL, PRIMARY KEY(JobId,AnfrageId));
  """; command.ExecuteNonQuery();
 }
}
