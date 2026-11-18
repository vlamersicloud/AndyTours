namespace AndyTours.Tests.Sicherung;

using System.Reflection;
using Xunit;

public sealed class BackendStartsicherungsStrukturTests
{
    [Fact]
    public void StartupBackupUsesLifecycleBeforeHostedServiceStart()
    {
        var source = ReadResource("AndyTours.Sicherung.Erstellung.Program.cs");
        var service = ReadResource("AndyTours.BackendStartsicherungsHostedService.cs");
        Assert.Contains("AddHostedService<BackendStartsicherungsHostedService>()", source, StringComparison.Ordinal);
        Assert.Contains("AddHostedService<GarminImportDienst>()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRequiredService<BackendStartsicherungsKoordinator>()", source, StringComparison.Ordinal);
        Assert.Contains("IHostedLifecycleService", service, StringComparison.Ordinal);
        Assert.Contains("StartingAsync(CancellationToken cancellationToken)", service, StringComparison.Ordinal);
        Assert.Contains(".Ausfuehren(cancellationToken)", service, StringComparison.Ordinal);
        Assert.DoesNotContain("FuehreStartsicherungDurch", source, StringComparison.Ordinal);

        var coordinator = ReadResource("AndyTours.BackendStartsicherungsKoordinator.cs");
        var sharedRules = ReadResource("AndyTours.WiederherstellungsvorschauRegeln.cs");
        Assert.Contains("WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig(backup)", coordinator, StringComparison.Ordinal);
        Assert.Contains("LegacyZeitstempel is null", sharedRules, StringComparison.Ordinal);
    }

    [Fact]
    public void CounterStoreOpensOnlyExistingReadWriteDatabaseAndNeverBootstraps()
    {
        var source = ReadResource("AndyTours.SqliteBackendStartsicherungsZaehler.cs");
        Assert.Contains("SqliteOpenMode.ReadWrite", source, StringComparison.Ordinal);
        Assert.Contains("Pooling = false", source, StringComparison.Ordinal);
        Assert.Contains("File.Exists(_databasePath)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OeffneDatenbank", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FuehreStartsicherungDurch", source, StringComparison.Ordinal);
        Assert.Contains("UPDATE Einstellung SET Wert = '0'", source, StringComparison.Ordinal);
    }

    private static string ReadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing test source resource: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
