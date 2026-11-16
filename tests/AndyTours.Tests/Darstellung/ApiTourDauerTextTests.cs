using AndyTours.App.ViewModels;

namespace AndyTours.Tests.Darstellung;

public sealed class ApiTourDauerTextTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData(0L, "0:00:00")]
    [InlineData(1L, "0:00:01")]
    [InlineData(59L, "0:00:59")]
    [InlineData(60L, "0:01:00")]
    [InlineData(3599L, "0:59:59")]
    [InlineData(3600L, "1:00:00")]
    [InlineData(90061L, "25:01:01")]
    [InlineData(long.MaxValue, "2562047788015215:30:07")]
    public void Formats_literal_seconds_without_hour_limit(long? seconds, string expected) =>
        Assert.Equal(expected, ApiTourDauerText.Formatiere(seconds));

    [Fact]
    public void Rejects_negative_seconds() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ApiTourDauerText.Formatiere(-1));
}