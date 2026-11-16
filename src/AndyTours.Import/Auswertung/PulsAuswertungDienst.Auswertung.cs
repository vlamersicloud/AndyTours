using AndyTours.Application.Auswertung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;

namespace AndyTours.Import.Auswertung;

public sealed partial class PulsAuswertungDienst : IPulsAuswertung
{
    public async Task<PulsAuswertungAntwort> Auswerten(
        PulsAuswertungsAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (anfrage is null || anfrage.Von > anfrage.Bis || !Enum.IsDefined(anfrage.Raster) ||
            anfrage.AktivitaetId is <= 0 || anfrage.KategorieId is <= 0 ||
            (anfrage.ProfilSport is not null && string.IsNullOrWhiteSpace(anfrage.ProfilSport)))
            throw Ungueltig();

        try
        {
            var source = await _quelle.Laden(anfrage, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var raster = MappaRaster(anfrage.Raster);
            var periods = BerechneAktivitaetsperioden.Ausfuehren(
                source.Touren, anfrage.Von, anfrage.Bis, raster);
            var reference = source.Referenz;
            GarminPulsprofil? profile = null;
            if (anfrage.ProfilSport is not null)
            {
                var matches = reference?.Profile.Where(p => string.Equals(p.Sport, anfrage.ProfilSport, StringComparison.Ordinal)).ToArray()
                    ?? Array.Empty<GarminPulsprofil>();
                if (matches.Length != 1) throw Konflikt();
                profile = matches[0];
            }

            var buckets = new Dictionary<(DateOnly Period, int Area), double>();
            var measuredPerPeriod = new Dictionary<DateOnly, double>();
            double measuredTotal = 0;
            int withoutData = 0;
            foreach (var tour in source.Touren)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = FindeAufzeichnung.Ausfuehren(tour);
                if (string.IsNullOrWhiteSpace(relative))
                {
                    withoutData++;
                    continue;
                }

                try
                {
                    using var snapshot = await _fit.VerlaufLesen(relative, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    var intervals = BerechnePulszeiten.Ausfuehren(snapshot.Punkte, profile);
                    if (intervals.Count == 0)
                    {
                        withoutData++;
                        continue;
                    }
                    var period = BerechneAktivitaetsperioden.Beginn(tour.Datum, raster);
                    foreach (var (area, seconds) in intervals)
                    {
                        var key = (period, area);
                        buckets[key] = buckets.GetValueOrDefault(key) + seconds;
                        measuredPerPeriod[period] = measuredPerPeriod.GetValueOrDefault(period) + seconds;
                        measuredTotal += seconds;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (StammdatenException) { withoutData++; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
                { withoutData++; }
            }

            cancellationToken.ThrowIfCancellationRequested();
            var labels = new SortedSet<int>(buckets.Keys.Select(k => k.Area));
            var areas = labels.Select(area => new PulsbereichAntwort(area,
                profile is null ? $"{area / 10}×10 bpm" : $"Zone {area}",
                profile is null ? $"{area}–{area + 9} bpm" : profile.Bereich(area))).ToArray();
            var periodDtos = periods.Select(period =>
            {
                var key = BerechneAktivitaetsperioden.Beginn(period.Von, raster);
                return new PulsperiodeAntwort(period.Von, period.Bis, period.Beschriftung) { Schluessel = key };
            }).ToArray();
            var cells = buckets.OrderBy(pair => pair.Key.Period).ThenBy(pair => pair.Key.Area)
                .Select(pair => new PulszelleAntwort(pair.Key.Period, pair.Key.Area, pair.Value,
                    measuredPerPeriod.GetValueOrDefault(pair.Key.Period) > 0
                        ? pair.Value / measuredPerPeriod[pair.Key.Period] : 0))
                .ToArray();
            var referenceAnswer = MappaReferenz(reference, source.ReferenzVersion);
            return new PulsAuswertungAntwort(anfrage.Von, anfrage.Bis, anfrage.Raster,
                source.Touren.Count, withoutData, measuredTotal,
                source.Touren.Sum(t => t.Fahrzeit.TotalSeconds),
                referenceAnswer, profile?.Sport, periodDtos, areas, cells);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (StammdatenException) { throw; }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            throw Ungueltig();
        }
    }
}
