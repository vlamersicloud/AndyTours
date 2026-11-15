using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using Microsoft.Data.Sqlite;

namespace AndyTours.Tests.Daten;

/// <summary>Tests für wiederholbare Tourenanlagen mit SQLite-Idempotenzbelegen.</summary>
public sealed class TourenApiIdempotenzTests : IDisposable
{
    private readonly string _ordner = Path.Combine(Path.GetTempPath(), "AndyTours-Idempotenz-" + Guid.NewGuid().ToString("N"));
    private readonly string _datenbank;
    private readonly SqliteTourenSpeicher _speicher;
    private readonly long _aktivitaetId;

    public TourenApiIdempotenzTests()
    {
        Directory.CreateDirectory(_ordner);
        _datenbank = Path.Combine(_ordner, "andytours.db");
        _speicher = new SqliteTourenSpeicher(_datenbank);
        using var verbindung = OeffneDatenbank.Ausfuehren(_datenbank);
        using var transaktion = verbindung.BeginTransaction();
        _aktivitaetId = AktivitaetenTabelle.Einfuegen(verbindung, transaktion, new Aktivitaet { Name = "Rad" });
        transaktion.Commit();
    }

    [Fact]
    public async Task GleicheAnfrageIdUndWerte_LiefertDieselbeTourOhneDuplikat()
    {
        var id = Guid.NewGuid();
        var werte = Werte();

        var zuerst = await _speicher.AnlegenIdempotent(werte, id);
        var erneut = await _speicher.AnlegenIdempotent(werte, id);

        Assert.Equal(zuerst.Id, erneut.Id);
        Assert.Equal(zuerst.Version, erneut.Version);
        Assert.Equal(1, (await _speicher.Liste(0, 50, null, null)).Gesamt);
    }

    [Fact]
    public async Task GleicheAnfrageIdMitAnderenWerten_WirdAbgelehnt()
    {
        var id = Guid.NewGuid();
        var zuerst = await _speicher.AnlegenIdempotent(Werte(), id);

        await Assert.ThrowsAsync<UngueltigeTourException>(() =>
            _speicher.AnlegenIdempotent(Werte() with { Name = "Andere Tour" }, id));

        Assert.Equal(1, (await _speicher.Liste(0, 50, null, null)).Gesamt);
        Assert.Equal(zuerst.Id, (await _speicher.Lesen(zuerst.Id))!.Id);
    }

    [Fact]
    public async Task FremdschluesselfehlerRolltReceiptZurueck_KorrigierterRetryKannAnlegen()
    {
        var id = Guid.NewGuid();
        var fehlerhafteWerte = Werte() with { KategorieIds = [long.MaxValue] };

        await Assert.ThrowsAsync<SqliteException>(() => _speicher.AnlegenIdempotent(fehlerhafteWerte, id));
        Assert.Equal(0, (await _speicher.Liste(0, 50, null, null)).Gesamt);

        var erfolgreich = await _speicher.AnlegenIdempotent(Werte() with { Name = "Korrigiert" }, id);

        Assert.Equal("Korrigiert", erfolgreich.Werte.Name);
        Assert.Equal(1, (await _speicher.Liste(0, 50, null, null)).Gesamt);
    }

    private TourWerte Werte() =>
        TourenMapper.ToAntwort(new Trainingseinheit
        {
            AktivitaetId = _aktivitaetId,
            Datum = new DateOnly(2026, 1, 1),
            Name = "Test",
            DistanzKm = 10,
            Fahrzeit = TimeSpan.FromMinutes(30)
        }).Werte;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_ordner)) Directory.Delete(_ordner, recursive: true);
    }
}
