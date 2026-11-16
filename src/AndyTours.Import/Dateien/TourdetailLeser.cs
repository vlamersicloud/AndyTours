using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
namespace AndyTours.Import.Dateien;

public sealed class TourdetailLeser : ITourdetailLeser
{
    private readonly ITourdetailQuelldaten _quelldaten;
    private readonly FitDateiDienst _dateien;

    public TourdetailLeser(ITourdetailQuelldaten quelldaten, string datenordner)
    {
        _quelldaten = quelldaten ?? throw new ArgumentNullException(nameof(quelldaten));
        _dateien = new FitDateiDienst(datenordner, TimeZoneInfo.Utc);
    }

    public async Task<TourdetailAntwort> Laden(TourdetailAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (anfrage?.TourIds is null || anfrage.TourIds.Count is < 1 or > 10000)
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Tourauswahl ungültig.");
        var bestand = await _quelldaten.Laden(anfrage.TourIds, cancellationToken);
        var etappen = new List<TourdetailEtappeAntwort>(bestand.Etappen.Count);
        foreach (var quelle in bestand.Etappen)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (quelle.RelativerFitPfad is null)
            { etappen.Add(new(quelle.Tour, null, null, "keine_aufzeichnung")); continue; }
            try
            {
                using var snapshot = await _dateien.VerlaufLesen(quelle.RelativerFitPfad, cancellationToken);
                var punkte = snapshot.Punkte.Select(p => {
                    if (!double.IsFinite(p.DistanzKm) || p.DistanzKm < 0 || p.Fahrzeit < TimeSpan.Zero ||
                        (p.Hoehe is double h && !double.IsFinite(h)) || (p.Geschwindigkeit is double g && !double.IsFinite(g)) ||
                        (p.Position is Geoposition pos && (!double.IsFinite(pos.Breite) || !double.IsFinite(pos.Laenge) || pos.Breite is < -90 or > 90 || pos.Laenge is < -180 or > 180)))
                        throw new StammdatenException(StammdatenFehlerart.Ungueltig, "FIT-Verlauf ungültig.");
                    return new TourdetailMesspunkt(p.ZeitpunktUtc is DateTime utc ? new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)) : null, p.DistanzKm, p.Fahrzeit.Ticks / TimeSpan.TicksPerMillisecond, p.Hoehe, p.Puls, p.Geschwindigkeit, p.Trittfrequenz, p.Temperatur, p.Position is Geoposition geo ? new(geo.Breite, geo.Laenge) : null);
                }).ToArray();
                etappen.Add(new(quelle.Tour, snapshot.DateiHash, punkte, null));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is StammdatenException or IOException or UnauthorizedAccessException or ArgumentException)
            { etappen.Add(new(quelle.Tour, null, null, "fit_nicht_lesbar")); }
        }
        cancellationToken.ThrowIfCancellationRequested();
        var canonical = JsonSerializer.SerializeToUtf8Bytes(etappen.Select(e => new { e.Tour.Id, e.Tour.Version, e.DateiVersion, e.Warncode }));
        return new(Convert.ToHexString(SHA256.HashData(canonical)), etappen);
    }
}
