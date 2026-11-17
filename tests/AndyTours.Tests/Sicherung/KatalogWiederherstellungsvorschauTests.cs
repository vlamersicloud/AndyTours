namespace AndyTours.Tests.Sicherung;

using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using System.IO;
using Xunit;

public sealed class KatalogWiederherstellungsvorschauTests
{
    private static readonly Guid FullId = Guid.Parse("4e1f6d1c-1284-4a7e-a95b-2e7b6215ed10");
    private static readonly Guid LegacyId = Guid.Parse("5e1f6d1c-1284-4a7e-a95b-2e7b6215ed11");
    private static readonly DateTimeOffset FullUtc = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string LegacyZeit = "2024-02-29_12-34-56";

    [Fact]
    public async Task VollarchivLiefertNurKatalogmetadatenUndUnveraenderlicheHinweise()
    {
        var eintrag = new SicherungAntwort(FullId, Sicherungsart.Vollarchiv, FullUtc, 123, Hash);
        var fake = new FakeKatalog(new SicherungListeAntwort([eintrag]));
        var service = new KatalogWiederherstellungsvorschau(fake);

        var antwort = await service.Pruefen(new WiederherstellungsvorschauAnfrage(FullId, Hash), default);

        Assert.Same(eintrag, antwort.Sicherung);
        Assert.Null(antwort.ManifestFormatversion);
        Assert.Null(antwort.Dateianzahl);
        Assert.Null(antwort.NutzdatenBytes);
        Assert.False(antwort.DatenbankGeprueft);
        Assert.False(antwort.AnwendungFreigegeben);
        Assert.True(((IList<string>)antwort.Hinweise).IsReadOnly);
        Assert.Equal(1, fake.AnzahlAufrufe);
    }

    [Fact]
    public async Task LegacyZeigtNurBekannteDatenbankmetadatenOhneRestorefreigabe()
    {
        var eintrag = new SicherungAntwort(LegacyId, Sicherungsart.LegacySqlite, null, 456, Hash)
            { LegacyZeitstempel = LegacyZeit };
        var service = new KatalogWiederherstellungsvorschau(new FakeKatalog(new SicherungListeAntwort([eintrag])));

        var antwort = await service.Pruefen(new WiederherstellungsvorschauAnfrage(LegacyId, Hash), default);

        Assert.Null(antwort.ManifestFormatversion);
        Assert.Equal(1, antwort.Dateianzahl);
        Assert.Equal(456L, antwort.NutzdatenBytes);
        Assert.False(antwort.DatenbankGeprueft);
        Assert.False(antwort.AnwendungFreigegeben);
        Assert.Contains(antwort.Hinweise, h => h.Contains("FIT-Dateien", StringComparison.Ordinal));
        Assert.Contains(antwort.Hinweise, h => h.Contains("Zeitzonenzeitpunkt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UngueltigeAnfrageWirdVorKatalogaufrufAbgelehnt()
    {
        var fake = new FakeKatalog(new SicherungListeAntwort([]));
        var service = new KatalogWiederherstellungsvorschau(fake);
        var ex = await Assert.ThrowsAsync<StammdatenException>(() =>
            service.Pruefen(new WiederherstellungsvorschauAnfrage(Guid.Empty, Hash), default));
        Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
        Assert.Equal(0, fake.AnzahlAufrufe);
    }

    [Fact]
    public async Task FehlendeDoppelteUndVeralteteEintraegeWerdenAbgelehnt()
    {
        var missing = new KatalogWiederherstellungsvorschau(new FakeKatalog(new SicherungListeAntwort([])));
        var notFound = await Assert.ThrowsAsync<StammdatenException>(() =>
            missing.Pruefen(new WiederherstellungsvorschauAnfrage(FullId, Hash), default));
        Assert.Equal(StammdatenFehlerart.NichtGefunden, notFound.Art);

        var entry = new SicherungAntwort(FullId, Sicherungsart.Vollarchiv, FullUtc, 123, Hash);
        var duplicate = new KatalogWiederherstellungsvorschau(new FakeKatalog(new SicherungListeAntwort([entry, entry])));
        var conflict = await Assert.ThrowsAsync<StammdatenException>(() =>
            duplicate.Pruefen(new WiederherstellungsvorschauAnfrage(FullId, Hash), default));
        Assert.Equal(StammdatenFehlerart.Konflikt, conflict.Art);

        var stale = new KatalogWiederherstellungsvorschau(new FakeKatalog(new SicherungListeAntwort([entry])));
        var changed = await Assert.ThrowsAsync<StammdatenException>(() =>
            stale.Pruefen(new WiederherstellungsvorschauAnfrage(FullId, new string('A', 64)), default));
        Assert.Equal(StammdatenFehlerart.Konflikt, changed.Art);
    }

    [Theory]
    [MemberData(nameof(UngueltigeKataloge))]
    public async Task FehlerhafteKatalogdatenWerdenAlsNichtVerfuegbarBehandelt(SicherungListeAntwort? katalogAntwort)
    {
        var fake = new FakeKatalog(katalogAntwort);
        var service = new KatalogWiederherstellungsvorschau(fake);
        var ex = await Assert.ThrowsAsync<SicherungskatalogNichtVerfuegbarException>(() =>
            service.Pruefen(new WiederherstellungsvorschauAnfrage(FullId, Hash), default));
        Assert.Equal(1, fake.AnzahlAufrufe);
        Assert.DoesNotContain("PRIVATE", ex.Message);
    }

    public static IEnumerable<object?[]> UngueltigeKataloge()
    {
        yield return [null];
        yield return [new SicherungListeAntwort(null!)];
        yield return [new SicherungListeAntwort([null!])];
        yield return [new SicherungListeAntwort([new SicherungAntwort(FullId, (Sicherungsart)99, FullUtc, 123, Hash)])];
        yield return [new SicherungListeAntwort([new SicherungAntwort(FullId, Sicherungsart.Vollarchiv, FullUtc, 123, "PRIVATE")])];
        yield return [new SicherungListeAntwort([new SicherungAntwort(FullId, Sicherungsart.Vollarchiv, FullUtc, -1, Hash)])];
        yield return [new SicherungListeAntwort([new SicherungAntwort(FullId, Sicherungsart.Vollarchiv, FullUtc, 0, Hash)])];
        yield return [new SicherungListeAntwort([new SicherungAntwort(FullId, Sicherungsart.Vollarchiv, null, 123, Hash)])];
        yield return [new SicherungListeAntwort([new SicherungAntwort(FullId, Sicherungsart.Vollarchiv, FullUtc.ToOffset(TimeSpan.FromHours(1)), 123, Hash)])];
        yield return [new SicherungListeAntwort([new SicherungAntwort(FullId, Sicherungsart.LegacySqlite, FullUtc, 123, Hash) { LegacyZeitstempel = LegacyZeit }])];
        yield return [new SicherungListeAntwort([new SicherungAntwort(FullId, Sicherungsart.LegacySqlite, null, 123, Hash) { LegacyZeitstempel = "2023-02-29_12-34-56" }])];
    }

    [Fact]
    public async Task VorabAbbruchErreichtKatalogNicht()
    {
        var fake = new FakeKatalog(new SicherungListeAntwort([]));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new KatalogWiederherstellungsvorschau(fake).Pruefen(new WiederherstellungsvorschauAnfrage(FullId, Hash), cts.Token));
        Assert.Equal(0, fake.AnzahlAufrufe);
    }

    [Fact]
    public async Task KatalogfehlerWerdenUnveraendertDurchgereicht()
    {
        var error = new IOException("PRIVATE path detail");
        var fake = new FakeKatalog(null, error);
        var thrown = await Assert.ThrowsAsync<IOException>(() =>
            new KatalogWiederherstellungsvorschau(fake).Pruefen(new WiederherstellungsvorschauAnfrage(FullId, Hash), default));
        Assert.Same(error, thrown);
    }

    [Fact]
    public async Task CancellationTokenWirdWeitergegeben()
    {
        using var cts = new CancellationTokenSource();
        var fake = new FakeKatalog(new SicherungListeAntwort([new SicherungAntwort(FullId, Sicherungsart.Vollarchiv, FullUtc, 123, Hash)]));

        await new KatalogWiederherstellungsvorschau(fake).Pruefen(new WiederherstellungsvorschauAnfrage(FullId, Hash), cts.Token);

        Assert.Equal(cts.Token, fake.UebergebenesToken);
    }

    [Fact]
    public async Task PortCancellationWirdNichtUmgewandelt()
    {
        var fake = new FakeKatalog(null, new OperationCanceledException("port canceled"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new KatalogWiederherstellungsvorschau(fake).Pruefen(new WiederherstellungsvorschauAnfrage(FullId, Hash), default));
    }

    private sealed class FakeKatalog(SicherungListeAntwort? antwort, Exception? fehler = null) : ISicherungskatalog
    {
        public int AnzahlAufrufe { get; private set; }
        public CancellationToken? UebergebenesToken { get; private set; }

        public Task<SicherungListeAntwort> Liste(CancellationToken cancellationToken)
        {
            AnzahlAufrufe++;
            UebergebenesToken = cancellationToken;
            if (fehler is not null) return Task.FromException<SicherungListeAntwort>(fehler);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(antwort!);
        }
    }
}
