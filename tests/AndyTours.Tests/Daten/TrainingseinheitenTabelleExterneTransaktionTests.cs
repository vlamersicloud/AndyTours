using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AndyTours.Tests.Daten;

public sealed class TrainingseinheitenTabelleExterneTransaktionTests
{
    private sealed class Fixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TourExternalTx-" + Guid.NewGuid().ToString("N") + ".db");
        public Fixture()
        {
            using var db = OeffneDatenbank.Ausfuehren(Path);
            using var tx = db.BeginTransaction();
            AktivitaetenTabelle.Einfuegen(db, tx, new Aktivitaet { Name = "Rad", IstHaupt = true });
            tx.Commit();
        }
        public void Dispose() { try { File.Delete(Path); } catch (IOException) { } }
    }

    [Fact]
    public void RollbackEntferntTourFahrerAnhangUndGarminZuordnungGemeinsam()
    {
        using var f = new Fixture();
        using var db = OeffneDatenbank.Ausfuehren(f.Path);
        using var tx = db.BeginTransaction();
        var tour = new Trainingseinheit { Datum = new DateOnly(2026, 5, 1), Name = "fixture", AktivitaetId = 1 };
        var attachment = new Anhang { Typ = AnhangTyp.GpsTrack, RelativerPfad = "synthetic.fit" };
        tour.Anhaenge.Add(attachment);
        var result = TrainingseinheitenTabelle.Einfuegen(db, tx, tour,
            (connection, transaction, id) => GarminImportZuordnung.Speichern(connection, transaction, 9001, id));
        Assert.True(result.Id > 0);
        Assert.True(result.FahrerId > 0);
        Assert.True(attachment.Id > 0);
        tx.Rollback();
        Assert.Equal(0, Scalar(db, "SELECT COUNT(*) FROM Trainingseinheit;"));
        Assert.Equal(0, Scalar(db, "SELECT COUNT(*) FROM Fahrer;"));
        Assert.Equal(0, Scalar(db, "SELECT COUNT(*) FROM Anhang;"));
        Assert.Null(EinstellungTabelle.Lesen(db, "GarminConnect.Aktivitaet.9001"));
    }

    [Fact]
    public void CommitBestaetigtTourUndZuordnungZusammen()
    {
        using var f = new Fixture();
        long id;
        using (var db = OeffneDatenbank.Ausfuehren(f.Path))
        using (var tx = db.BeginTransaction())
        {
            var result = TrainingseinheitenTabelle.Einfuegen(db, tx,
                new Trainingseinheit { Datum = new DateOnly(2026, 5, 2), Name = "fixture", AktivitaetId = 1 },
                (connection, transaction, tourId) => GarminImportZuordnung.Speichern(connection, transaction, 9002, tourId));
            id = result.Id;
            tx.Commit();
        }
        using var verify = OeffneDatenbank.Ausfuehren(f.Path);
        Assert.Equal(1, Scalar(verify, "SELECT COUNT(*) FROM Trainingseinheit WHERE Id=$id;", ("$id", id)));
        Assert.Equal(id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            EinstellungTabelle.Lesen(verify, "GarminConnect.Aktivitaet.9002"));
    }

    private static long Scalar(SqliteConnection db, string sql, params (string Name, object Value)[] args)
    {
        using var command = db.CreateCommand();
        command.CommandText = sql;
        foreach (var arg in args) command.Parameters.AddWithValue(arg.Name, arg.Value);
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
