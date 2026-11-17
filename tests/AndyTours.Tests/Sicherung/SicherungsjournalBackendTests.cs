namespace AndyTours.Tests.Sicherung;

using System.Security.Cryptography;
using System.Text;
using AndyTours.Client.Sicherung;
using Xunit;

public sealed class SicherungsjournalBackendTests
{
    [Fact]
    public void ReturnsExpectedUppercaseSha256ForCanonicalBaseAddress()
    {
        const string canonical = "https://example.com/api/";
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var actual = SicherungsjournalBackend.Kennung(new Uri("https://EXAMPLE.com:443/api"));
        Assert.Equal(expected, actual);
        Assert.Equal(64, actual.Length);
        Assert.All(actual, character => Assert.Contains(character, "0123456789ABCDEF"));
        Assert.DoesNotContain("example.com", actual, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api", actual, StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalizesSchemeHostDefaultPortAndTrailingSlashButPreservesPathCase()
    {
        var canonical = SicherungsjournalBackend.Kennung(new Uri("https://example.com/api/"));
        Assert.Equal(canonical, SicherungsjournalBackend.Kennung(new Uri("HTTPS://EXAMPLE.COM:443/api")));
        Assert.NotEqual(canonical, SicherungsjournalBackend.Kennung(new Uri("https://example.com/api//")));
        Assert.Equal(SicherungsjournalBackend.Kennung(new Uri("https://example.com/")),
            SicherungsjournalBackend.Kennung(new Uri("https://EXAMPLE.COM:443")));
        Assert.NotEqual(canonical, SicherungsjournalBackend.Kennung(new Uri("https://example.com/API")));
        Assert.NotEqual(canonical, SicherungsjournalBackend.Kennung(new Uri("http://example.com/api")));
        Assert.NotEqual(canonical, SicherungsjournalBackend.Kennung(new Uri("https://example.com:444/api")));
    }

    [Fact]
    public void EscapedPathIsHandledDeterministicallyWithoutRawUriDisclosure()
    {
        var uri = new Uri("https://example.com/a%2Fb/");
        var first = SicherungsjournalBackend.Kennung(uri);
        var second = SicherungsjournalBackend.Kennung(new Uri("https://EXAMPLE.com:443/a%2Fb"));
        Assert.Equal(first, second);
        Assert.DoesNotContain("%2F", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.com", first, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("ftp://example.com/")]
    [InlineData("file:///tmp/backup")]
    [InlineData("https://user:secret@example.com/api/")]
    [InlineData("https://example.com/api/?token=secret")]
    [InlineData("https://example.com/api/#fragment")]
    public void RejectsUnsupportedOrCredentialBearingAddressesWithoutEchoingThem(string raw)
    {
        var error = Assert.Throws<ArgumentException>(() => SicherungsjournalBackend.Kennung(new Uri(raw, UriKind.RelativeOrAbsolute)));
        Assert.DoesNotContain(raw, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsNullAddress()
    {
        var error = Assert.Throws<ArgumentNullException>(() => SicherungsjournalBackend.Kennung(null!));
        Assert.DoesNotContain("https://", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
