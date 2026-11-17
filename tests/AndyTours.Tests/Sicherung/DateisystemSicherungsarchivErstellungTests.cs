namespace AndyTours.Tests.Sicherung;

using System.Security.Cryptography;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Daten;
using AndyTours.Daten.Datensicherung;
using Xunit;

public sealed class DateisystemSicherungsarchivErstellungTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "andy-creator-tests", Guid.NewGuid().ToString("N"));
    private string Root => Path.Combine(_base, "data");
    private string Backup => Path.Combine(Root, "Sicherungen");
    private string Db => Path.Combine(Root, "andytours.db");
    private string FinalPath(Guid id) => Path.Combine(Backup, $"andytours_full_{id:N}.zip");
    private static readonly DateTimeOffset Created = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly SicherungsmanifestGrenzen ManifestLimits = new(100, 1024L * 1024 * 1024, 1024L * 1024 * 1024, 200);

    public DateisystemSicherungsarchivErstellungTests()
    {
        Directory.CreateDirectory(Path.Combine(Root, "2026"));
        Directory.CreateDirectory(Backup);
        File.WriteAllBytes(Path.Combine(Root, "2026", "ride.fit"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(Root, "orphan.bin"), [4]);
        using var connection = OeffneDatenbank.Ausfuehren(Db);
        using var transaction = connection.BeginTransaction();
        var driver = FahrerTabelle.Standardfahrer(connection, transaction);
        var activity = AktivitaetenTabelle.Einfuegen(connection, transaction, new Aktivitaet { Name = "Rad", IstHaupt = true });
        var tour = new Trainingseinheit
        {
            FahrerId = driver, AktivitaetId = activity, Datum = new DateOnly(2026, 1, 1), Name = "seed",
            Fahrzeit = TimeSpan.FromMinutes(1), DistanzKm = 1, Hoehenmeter = 1, MaxHoehe = 1,
            StartHoehe = 1, MaxGeschwindigkeit = 1, HfDurchschnitt = 1, HfMax = 1, HfMin = 1,
            Trittfrequenz = 1, Temperatur = 1, LeistungWatt = 1, KoerpergewichtKg = 1,
            Erholungspuls = 1, Trainingsbereich = "test", Kommentar = "test", KategorieIds = []
        };
        var id = TrainingseinheitenTabelle.Einfuegen(connection, transaction, tour).Id;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO Anhang (EinheitId, Typ, RelativerPfad) VALUES ($id, 'GpsTrack', '2026/ride.fit');";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    [Fact]
    public async Task CreatesInspectedArchiveAndRetryReturnsSameImmutableFile()
    {
        var dbBefore = File.ReadAllBytes(Db);
        var ridePath = Path.Combine(Root, "2026", "ride.fit");
        var rideBefore = File.ReadAllBytes(ridePath);
        var orphanBefore = File.ReadAllBytes(Path.Combine(Root, "orphan.bin"));
        var id = Guid.NewGuid();
        var service = CreateService();
        var created = await service.Erstellen(new SicherungsarchivErstellungsanfrage(id), CancellationToken.None);
        var finalPath = FinalPath(id);
        Assert.False(created.BereitsVorhanden);
        Assert.Equal(id, created.Sicherung.Id);
        Assert.Equal(Sicherungsart.Vollarchiv, created.Sicherung.Art);
        Assert.Equal(Created, created.Sicherung.ErstelltUtc);
        var finalBytes = File.ReadAllBytes(finalPath);
        Assert.Equal(finalBytes.LongLength, created.Sicherung.Byteanzahl);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(finalBytes)), created.Sicherung.InhaltHash);
        using (var input = new MemoryStream(finalBytes, writable: false))
        {
            var verified = await SicherungsarchivPruefung.Pruefe(input, ManifestLimits, ["Sicherungen"],
                20_000_000, 65_536, 1_000_000);
            Assert.Equal(3, verified.Eintraege.Count);
        }
        Assert.Equal(new[] { Path.GetFileName(finalPath) }, Directory.GetFiles(Backup).Select(Path.GetFileName));
        Assert.Empty(Directory.GetDirectories(Backup));
        Assert.Equal(dbBefore, File.ReadAllBytes(Db));
        Assert.Equal(rideBefore, File.ReadAllBytes(ridePath));
        Assert.Equal(orphanBefore, File.ReadAllBytes(Path.Combine(Root, "orphan.bin")));

        var retry = await service.Erstellen(new SicherungsarchivErstellungsanfrage(id), CancellationToken.None);
        Assert.True(retry.BereitsVorhanden);
        Assert.Equal(created.Sicherung, retry.Sicherung);
        Assert.Equal(finalBytes, File.ReadAllBytes(finalPath));

        var otherId = Guid.NewGuid();
        var other = await service.Erstellen(new SicherungsarchivErstellungsanfrage(otherId), CancellationToken.None);
        Assert.False(other.BereitsVorhanden);
        Assert.NotEqual(finalPath, FinalPath(otherId));
        Assert.True(File.Exists(FinalPath(otherId)));
    }

    [Fact]
    public async Task InvalidExistingFinalIsPreservedAndStaleClaimIsNotRemoved()
    {
        var invalidId = Guid.NewGuid();
        var invalidFinal = FinalPath(invalidId);
        var original = new byte[] { 9, 8, 7 };
        File.WriteAllBytes(invalidFinal, original);
        var service = CreateService();
        await AssertInvalid(() => service.Erstellen(new SicherungsarchivErstellungsanfrage(invalidId), CancellationToken.None));
        Assert.Equal(original, File.ReadAllBytes(invalidFinal));

        var busyId = Guid.NewGuid();
        var claim = Path.Combine(Backup, ".andytours-claim-" + busyId.ToString("N"));
        File.WriteAllText(claim, "stale");
        await AssertConflict(() => service.Erstellen(new SicherungsarchivErstellungsanfrage(busyId), CancellationToken.None));
        Assert.Equal("stale", File.ReadAllText(claim));
        Assert.False(File.Exists(FinalPath(busyId)));
        Assert.Equal(2, Directory.GetFiles(Backup).Length);
        Assert.Empty(Directory.GetDirectories(Backup));
    }

    [Fact]
    public async Task MissingReferencedFileFailsWithoutPublishingOrLeavingWorkspace()
    {
        File.Delete(Path.Combine(Root, "2026", "ride.fit"));
        var id = Guid.NewGuid();
        await AssertInvalid(() => CreateService().Erstellen(new SicherungsarchivErstellungsanfrage(id), CancellationToken.None));
        Assert.False(File.Exists(FinalPath(id)));
        Assert.Empty(Directory.GetFiles(Backup));
        Assert.Empty(Directory.GetDirectories(Backup));
    }

    [Fact]
    public async Task EnforcesArchiveAndManifestLimitsAndCancellationAndRejectsEmptyOperationId()
    {
        var archiveLimited = CreateService(maxArchive: 8);
        var archiveId = Guid.NewGuid();
        await AssertInvalid(() => archiveLimited.Erstellen(new SicherungsarchivErstellungsanfrage(archiveId), CancellationToken.None));
        AssertNoPublishedOrStaged(archiveId);

        var manifestLimited = CreateService(maxManifest: 1);
        var manifestId = Guid.NewGuid();
        await AssertInvalid(() => manifestLimited.Erstellen(new SicherungsarchivErstellungsanfrage(manifestId), CancellationToken.None));
        AssertNoPublishedOrStaged(manifestId);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelledId = Guid.NewGuid();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService().Erstellen(new SicherungsarchivErstellungsanfrage(cancelledId), cts.Token));
        AssertNoPublishedOrStaged(cancelledId);

        await AssertInvalid(() => CreateService().Erstellen(new SicherungsarchivErstellungsanfrage(Guid.Empty), CancellationToken.None));
        Assert.Empty(Directory.GetFiles(Backup));
        Assert.Empty(Directory.GetDirectories(Backup));
    }

    private DateisystemSicherungsarchivErstellung CreateService(long maxArchive = 20_000_000, long maxManifest = 65_536) =>
        new(Root, Backup, new SicherungsarchivGrenzen(ManifestLimits, maxArchive, maxManifest, 1_000_000), new FixedTimeProvider(Created));

    private void AssertNoPublishedOrStaged(Guid id)
    {
        Assert.False(File.Exists(FinalPath(id)));
        Assert.False(Directory.Exists(Path.Combine(Backup, ".andytours-stage-" + id.ToString("N"))));
        Assert.False(File.Exists(Path.Combine(Backup, ".andytours-claim-" + id.ToString("N"))));
    }

    private static async Task AssertInvalid(Func<Task<SicherungsarchivErstellungsantwort>> operation)
    {
        var ex = await Assert.ThrowsAsync<StammdatenException>(operation);
        Assert.Equal(StammdatenFehlerart.Ungueltig, ex.Art);
    }

    private static async Task AssertConflict(Func<Task<SicherungsarchivErstellungsantwort>> operation)
    {
        var ex = await Assert.ThrowsAsync<StammdatenException>(operation);
        Assert.Equal(StammdatenFehlerart.Konflikt, ex.Art);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    public void Dispose()
    {
        if (Directory.Exists(_base)) Directory.Delete(_base, recursive: true);
        GC.SuppressFinalize(this);
    }
}
