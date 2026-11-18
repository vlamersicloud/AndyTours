namespace AndyTours.Tests.Sicherung;

using AndyTours.Api;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class SqliteBasispruefungKonfigurationTests
{
    [Fact]
    public void ExplicitConfigurationRequiresExistingSeparateRootsAndExecutableFile()
    {
        using var temp = new TempRoot();
        var active = Directory.CreateDirectory(Path.Combine(temp.Path, "active")).FullName;
        var check = Directory.CreateDirectory(Path.Combine(temp.Path, "check")).FullName;
        var exe = Path.Combine(temp.Path, "checker-placeholder"); File.WriteAllText(exe, "fixture only; never executed");
        var config = Build(ValidValues(exe, check));
        Assert.True(SqliteBasispruefungKonfiguration.TryLesen(config, active, out var parsed));
        Assert.NotNull(parsed);
        Assert.False(SqliteBasispruefungKonfiguration.TryLesen(config, temp.Path, out _));
        var nested = Path.Combine(temp.Path, "active", "nested"); Directory.CreateDirectory(nested);
        Assert.False(SqliteBasispruefungKonfiguration.TryLesen(Build(ValidValues(exe, nested)), active, out _));
        Assert.False(SqliteBasispruefungKonfiguration.TryLesen(Build(ValidValues(exe, active)), nested, out _));
    }
    [Theory]
    [InlineData(null)] [InlineData("0")] [InlineData("-1")] [InlineData("999999999999999999999999")]
    public void MissingOrInvalidRuntimeLimitFailsClosed(string? value)
    {
        using var temp = new TempRoot();
        var active = Directory.CreateDirectory(Path.Combine(temp.Path, "active")).FullName;
        var check = Directory.CreateDirectory(Path.Combine(temp.Path, "check")).FullName;
        var exe = Path.Combine(temp.Path, "checker"); File.WriteAllText(exe, "not run");
        var values = ValidValues(exe, check);
        if (value is null) values.Remove("Backend:Sicherung:SqlitePruefung:LaufzeitMillisekunden");
        else values["Backend:Sicherung:SqlitePruefung:LaufzeitMillisekunden"] = value;
        Assert.False(SqliteBasispruefungKonfiguration.TryLesen(Build(values), active, out _));
    }
    private static IConfiguration Build(Dictionary<string,string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    private static Dictionary<string,string?> ValidValues(string exe, string check) => new()
    {
        ["Backend:Sicherung:SqlitePruefung:CheckerExecutable"] = exe,
        ["Backend:Sicherung:SqlitePruefung:Pruefordner"] = check,
        ["Backend:Sicherung:SqlitePruefung:LaufzeitMillisekunden"] = "1234",
        ["Backend:Sicherung:SqlitePruefung:MaxOutputBytes"] = "2048",
        ["Backend:Sicherung:SqlitePruefung:MaxDatenbankBytes"] = "4096",
        ["Backend:Sicherung:MaxEintraege"] = "10", ["Backend:Sicherung:MaxDateiBytes"] = "4096",
        ["Backend:Sicherung:MaxGesamtBytes"] = "8192", ["Backend:Sicherung:MaxPfadzeichen"] = "200",
        ["Backend:Sicherung:MaxArchivBytes"] = "16384", ["Backend:Sicherung:MaxManifestBytes"] = "4096",
        ["Backend:Sicherung:MaxZentralverzeichnisBytes"] = "2048"
    };
    private sealed class TempRoot : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AndyTours-check-config-" + Guid.NewGuid().ToString("N"));
        public TempRoot() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
