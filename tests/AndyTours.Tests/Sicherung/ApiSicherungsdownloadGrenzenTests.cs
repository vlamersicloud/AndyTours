using AndyTours.Api;

namespace AndyTours.Tests.Sicherung;

public sealed class ApiSicherungsdownloadGrenzenTests
{
    [Fact]
    public void DownloadLimitIsRequiredPositiveInvariantLongAndOverflowFailsClosed()
    {
        Assert.False(SicherungsdownloadGrenzen.TryParse(null, out _));
        Assert.False(SicherungsdownloadGrenzen.TryParse("", out _));
        Assert.False(SicherungsdownloadGrenzen.TryParse("0", out _));
        Assert.False(SicherungsdownloadGrenzen.TryParse("-1", out _));
        Assert.False(SicherungsdownloadGrenzen.TryParse("1,000", out _));
        Assert.False(SicherungsdownloadGrenzen.TryParse("9223372036854775808", out _));
        Assert.True(SicherungsdownloadGrenzen.TryParse("9223372036854775807", out var maximum));
        Assert.Equal(long.MaxValue, maximum);
    }
}
