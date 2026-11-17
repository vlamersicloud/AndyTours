namespace AndyTours.Application.Sicherung;

using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

/// <summary>Performs bounded, side-effect-free syntactic validation of a restore-preview request.</summary>
public static class WiederherstellungsvorschauRegeln
{
    private const string Ungueltig = "Sicherungsanforderung für die Vorschau ist ungültig.";

    /// <summary>Rejects invalid ID/hash syntax without accessing storage or revealing supplied values.</summary>
    public static WiederherstellungsvorschauAnfrage PruefeAnfrage(WiederherstellungsvorschauAnfrage anfrage)
    {
        if (anfrage is null || anfrage.SicherungsId == Guid.Empty ||
            anfrage.ErwarteterInhaltHash is not { Length: 64 } hash ||
            !hash.All(Uri.IsHexDigit))
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, Ungueltig);

        return anfrage with { ErwarteterInhaltHash = hash.ToUpperInvariant() };
    }

    /// <summary>Validates metadata-only response invariants against the exact requested ID and full-file hash.</summary>
    public static bool IstMetadatenAntwortGueltig(
        WiederherstellungsvorschauAntwort? antwort, WiederherstellungsvorschauAnfrage? anfrage)
    {
        if (antwort is null || anfrage is null || anfrage.SicherungsId == Guid.Empty ||
            anfrage.ErwarteterInhaltHash is not { Length: 64 } expectedHash || !expectedHash.All(Uri.IsHexDigit))
            return false;

        var eintrag = antwort.Sicherung;
        if (!IstKatalogeintragGueltig(eintrag) || eintrag.Id != anfrage.SicherungsId ||
            !string.Equals(eintrag.InhaltHash, expectedHash, StringComparison.OrdinalIgnoreCase) ||
            antwort.DatenbankGeprueft || antwort.AnwendungFreigegeben || antwort.Hinweise is null || antwort.Hinweise.Count == 0 ||
            antwort.Hinweise.Any(string.IsNullOrWhiteSpace))
            return false;

        return eintrag.Art switch
        {
            Sicherungsart.Vollarchiv => antwort.ManifestFormatversion is null && antwort.Dateianzahl is null && antwort.NutzdatenBytes is null,
            Sicherungsart.LegacySqlite => antwort.ManifestFormatversion is null && antwort.Dateianzahl == 1 && antwort.NutzdatenBytes == eintrag.Byteanzahl,
            _ => false
        };
    }

    /// <summary>Validates catalog entry shape only; this does not prove SQLite or restore validity.</summary>
    public static bool IstKatalogeintragGueltig(SicherungAntwort? eintrag)
    {
        if (eintrag is null || eintrag.Id == Guid.Empty || eintrag.Byteanzahl < 0 ||
            eintrag.InhaltHash is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit))
            return false;
        return eintrag.Art switch
        {
            Sicherungsart.Vollarchiv => eintrag.Byteanzahl > 0 && eintrag.ErstelltUtc is { Offset: { Ticks: 0 } } && eintrag.LegacyZeitstempel is null,
            Sicherungsart.LegacySqlite => eintrag.ErstelltUtc is null && LegacyZeitstempelGueltig(eintrag.LegacyZeitstempel),
            _ => false
        };
    }

    private static bool LegacyZeitstempelGueltig(string? wert) =>
        wert is { Length: 19 } && DateTime.TryParseExact(wert, "yyyy-MM-dd_HH-mm-ss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _);
}
