using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Import.Garmin;

namespace AndyTours.Import.Auswertung;

public sealed partial class PulsAuswertungDienst
{
    public async Task<PulsReferenzAntwort> AktualisierenReferenz(
        AndyTours.Contracts.V1.PulsReferenzAktualisierenAnfrage anfrage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (anfrage is null || anfrage.Version is not { Length: 64 } ||
            !anfrage.Version.All(Uri.IsHexDigit))
            throw Ungueltig();

        var current = await _quelle.ReferenzLaden(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(anfrage.Version, current.Version, StringComparison.OrdinalIgnoreCase))
            throw Konflikt();
        if (string.IsNullOrWhiteSpace(current.Schluessel))
            throw new StammdatenException(StammdatenFehlerart.Ungueltig,
                "Garmin-Schlüssel ist nicht konfiguriert.");

        try
        {
            using var client = _httpFactory();
            var personal = new GarminPersonalClient(client, current.Schluessel);
            var profiles = await personal.LesePulsprofileAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (profiles.Count == 0)
                throw new InvalidDataException("Empty Garmin pulse profile response.");
            var stand = new GarminPulszonenStand(_zeit.GetUtcNow(), profiles);
            var saved = await _quelle.ReferenzSpeichern(current.Version, stand, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return MappaReferenz(saved.Stand, saved.Version);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException)
        {
            throw new StammdatenException(StammdatenFehlerart.Ungueltig,
                "Garmin-Pulsreferenz konnte nicht aktualisiert werden.");
        }
        catch (StammdatenException) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or
                                   System.Text.Json.JsonException or ArgumentException or
                                   InvalidOperationException or TaskCanceledException)
        {
            throw new StammdatenException(StammdatenFehlerart.Ungueltig,
                "Garmin-Pulsreferenz konnte nicht aktualisiert werden.");
        }
    }
}
