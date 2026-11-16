using System.Text;
using AndyTours.Contracts.V1;
using AndyTours.Application.Stammdaten;

namespace AndyTours.Application.Planung;

public static class RoutingKonfigurationRegeln
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static void PruefeDienst(RoutingAnbieter dienst)
    {
        if (!Enum.IsDefined(dienst)) throw Invalid();
    }

    public static void PruefeVersion(string? version)
    {
        if (version is null || version.Length != 64 || !version.All(Uri.IsHexDigit))
            throw Invalid();
    }

    public static string Adresse(string? value)
    {
        string trimmed;
        try
        {
            StrictUtf8.GetByteCount(value ?? string.Empty);
            trimmed = (value ?? string.Empty).Trim();
        }
        catch (EncoderFallbackException) { throw Invalid(); }
        if (trimmed.Length is 0 or > 4096 || trimmed.Any(char.IsControl) ||
            !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw Invalid();
        return trimmed;
    }

    public static string OrsSchluessel(string? value)
    {
        string trimmed;
        try
        {
            StrictUtf8.GetByteCount(value ?? string.Empty);
            trimmed = (value ?? string.Empty).Trim();
        }
        catch (EncoderFallbackException) { throw Invalid(); }
        if (trimmed.Length is 0 or > 4096 || trimmed.Any(char.IsControl))
            throw Invalid();
        return trimmed;
    }

    public static bool IstAdresseSichtbar(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.Any(char.IsControl))
            return false;
        try { StrictUtf8.GetByteCount(value); }
        catch (EncoderFallbackException) { return false; }
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return false;
        return string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
    }

    private static StammdatenException Invalid() =>
        new(StammdatenFehlerart.Ungueltig, "Routingkonfiguration ungültig.");
}
