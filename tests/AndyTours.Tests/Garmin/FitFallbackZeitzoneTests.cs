using AndyTours.Import.Garmin;
using Xunit;

namespace AndyTours.Tests.Garmin;

[Collection(FitTestdatei.Testgruppe)]
public sealed class FitFallbackZeitzoneTests
{
    [Fact]
    public void ExplicitLocalTimestampHasPrecedenceOverInjectedFallback()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".fit");
        try
        {
            FitTestdatei.Schreibe(path, new FitTestdatei.Werte(new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc), VerschiebungStunden: 3));
            var (record, error) = LeseFitAufzeichnung.Ausfuehren(path, TimeZoneInfo.CreateCustomTimeZone("test-minus", TimeSpan.FromHours(-5), "test", "test"));
            Assert.Null(error);
            Assert.Equal(new TimeOnly(11, 0), record!.Startzeit);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void MissingLocalTimestampUsesInjectedFallbackInsteadOfHostTimezone()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".fit");
        try
        {
            FitTestdatei.Schreibe(path, new FitTestdatei.Werte(new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc), OhneOrtszeit: true));
            var plusTwo = TimeZoneInfo.CreateCustomTimeZone("test-plus-two", TimeSpan.FromHours(2), "test", "test");
            var (record, error) = LeseFitAufzeichnung.Ausfuehren(path, plusTwo);
            Assert.Null(error);
            Assert.Equal(new TimeOnly(10, 0), record!.Startzeit);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
