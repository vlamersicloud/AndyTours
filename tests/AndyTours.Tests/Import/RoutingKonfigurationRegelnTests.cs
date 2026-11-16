using AndyTours.Application.Planung;
using AndyTours.Contracts.V1;

namespace AndyTours.Tests.Import;

public sealed class RoutingKonfigurationRegelnTests
{
    [Theory]
    [InlineData("https://router.example/path", true)]
    [InlineData("http://router.example/path", true)]
    [InlineData("https://user:secret@router.example/path", false)]
    [InlineData("https://router.example/path?token=secret", false)]
    [InlineData("https://router.example/path#secret", false)]
    [InlineData("https://router.example/path\r\nX-Leak: yes", false)]
    [InlineData("https://router.example/\ud800", false)]
    public void Address_visibility_never_exposes_credentials_or_invalid_legacy_text(string value, bool expected) =>
        Assert.Equal(expected, RoutingKonfigurationRegeln.IstAdresseSichtbar(value));

    [Fact]
    public void Key_and_address_requests_keep_original_text_but_redact_ToString()
    {
        const string secret = "e\u0301-secret";
        Assert.Equal(secret, RoutingKonfigurationRegeln.OrsSchluessel($" {secret} "));
        Assert.Equal("https://router.example/a%2Fb", RoutingKonfigurationRegeln.Adresse(" https://router.example/a%2Fb "));
        var keyRequest = new RoutingOrsSchluesselAnfrage(new string('A', 64), secret);
        var addressRequest = new RoutingAdresseAnfrage(new string('B', 64), "https://user:secret@router.example");
        Assert.DoesNotContain(secret, keyRequest.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("user:secret", addressRequest.ToString(), StringComparison.Ordinal);
    }
}
