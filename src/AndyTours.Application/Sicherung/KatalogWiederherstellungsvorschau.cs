namespace AndyTours.Application.Sicherung;

using System.Collections.ObjectModel;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

/// <summary>Builds a non-destructive preview from verified catalog metadata only.</summary>
public sealed class KatalogWiederherstellungsvorschau : IWiederherstellungsvorschau
{
    private readonly ISicherungskatalog _katalog;
    private static readonly ReadOnlyCollection<string> FullHinweise = Array.AsReadOnly(new[]
    {
        "Manifestzusammenfassung und isolierte Datenbankprüfung liegen nicht vor.",
        "Keine Anwendungsfreigabe: Betriebs-Guard, zusätzliche Sicherung und ausdrückliche Bestätigung fehlen."
    });
    private static readonly ReadOnlyCollection<string> LegacyHinweise = Array.AsReadOnly(new[]
    {
        "Legacy-Sicherung enthält nur die Datenbank; FIT-Dateien und Anhänge sind nicht enthalten.",
        "Datenbankprüfung wurde nicht ausgeführt; kein genauer Zeitzonenzeitpunkt verfügbar.",
        "Keine Anwendungsfreigabe: Betriebs-Guard, zusätzliche Sicherung und ausdrückliche Bestätigung fehlen."
    });
    private const string NichtGefunden = "Sicherung für die Vorschau wurde nicht gefunden.";
    private const string Mehrdeutig = "Sicherungskatalog ist für die Vorschau nicht eindeutig.";
    private const string Geaendert = "Sicherung stimmt nicht mit dem erwarteten Inhalt überein.";

    public KatalogWiederherstellungsvorschau(ISicherungskatalog katalog) =>
        _katalog = katalog ?? throw new ArgumentNullException(nameof(katalog));

    public async Task<WiederherstellungsvorschauAntwort> Pruefen(
        WiederherstellungsvorschauAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var gepruefteAnfrage = WiederherstellungsvorschauRegeln.PruefeAnfrage(anfrage);
        var katalogAntwort = await _katalog.Liste(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (katalogAntwort?.Eintraege is not { } katalogEintraege)
            throw new SicherungskatalogNichtVerfuegbarException();

        SicherungAntwort? gefunden = null;
        foreach (var eintrag in katalogEintraege)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (eintrag is null)
                throw new SicherungskatalogNichtVerfuegbarException();
            if (eintrag.Id != gepruefteAnfrage.SicherungsId) continue;
            if (gefunden is not null)
                throw new StammdatenException(StammdatenFehlerart.Konflikt, Mehrdeutig);
            gefunden = eintrag;
        }
        if (gefunden is null)
            throw new StammdatenException(StammdatenFehlerart.NichtGefunden, NichtGefunden);
        if (!WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig(gefunden))
            throw new SicherungskatalogNichtVerfuegbarException();
        if (!string.Equals(gefunden.InhaltHash, gepruefteAnfrage.ErwarteterInhaltHash,
                StringComparison.OrdinalIgnoreCase))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, Geaendert);

        return gefunden.Art == Sicherungsart.Vollarchiv
            ? new WiederherstellungsvorschauAntwort(gefunden, null, null, null, false, false, FullHinweise)
            : new WiederherstellungsvorschauAntwort(gefunden, null, 1, gefunden.Byteanzahl, false, false, LegacyHinweise);
    }

}
