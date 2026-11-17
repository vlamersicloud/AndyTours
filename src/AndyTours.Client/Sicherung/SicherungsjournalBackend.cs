namespace AndyTours.Client.Sicherung;

using System.Security.Cryptography;
using System.Text;

/// <summary>Der nichtgeheime, kanonische Schlüssel für ein backendbezogenes lokales Sicherungsjournal.</summary>
public static class SicherungsjournalBackend
{
    /// <summary>
    /// Liefert den SHA-256-Schlüssel der kanonischen Basisadresse, ohne die Adresse selbst zu speichern.
    /// URI-Pfad bleibt case-sensitive; Scheme/Host/Standardport werden durch Uri kanonisiert.
    /// </summary>
    public static string Kennung(Uri basisAdresse)
    {
        ArgumentNullException.ThrowIfNull(basisAdresse);
        if (!basisAdresse.IsAbsoluteUri ||
            (!string.Equals(basisAdresse.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(basisAdresse.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrEmpty(basisAdresse.Host) ||
            !string.IsNullOrEmpty(basisAdresse.UserInfo) ||
            !string.IsNullOrEmpty(basisAdresse.Query) ||
            !string.IsNullOrEmpty(basisAdresse.Fragment))
            throw new ArgumentException("Backendadresse für Sicherungsjournal ungültig.");

        var canonical = basisAdresse.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);
        if (!canonical.EndsWith('/')) canonical += "/";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
