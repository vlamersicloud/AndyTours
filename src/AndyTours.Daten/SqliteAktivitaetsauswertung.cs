using AndyTours.Application.Auswertung;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Daten;
using AndyTours.Core.Modell;

namespace AndyTours.Daten;

/// <summary>Erzeugt aggregierte Aktivitätsauswertungen aus dem festgelegten SQLite-Datenbestand.</summary>
public sealed class SqliteAktivitaetsauswertung(string datenbankpfad) : IAktivitaetsauswertung
{
    /// <inheritdoc />
    public Task<AktivitaetsauswertungAntwort> Laden(
        AktivitaetsauswertungAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (anfrage is null || anfrage.Von > anfrage.Bis ||
            !Enum.IsDefined(anfrage.Raster) ||
            anfrage.AktivitaetId is <= 0 || anfrage.KategorieId is <= 0)
            throw new UngueltigeTourException("Filter oder Zeitraum der Auswertung sind ungültig.");

        try
        {
            using var verbindung = OeffneDatenbank.Ausfuehren(datenbankpfad);
            var einheiten = TrainingseinheitenTabelle.Laden(verbindung)
                .Where(t => !anfrage.AktivitaetId.HasValue || t.AktivitaetId == anfrage.AktivitaetId.Value)
                .Where(t => !anfrage.KategorieId.HasValue || t.KategorieIds.Contains(anfrage.KategorieId.Value))
                .ToList();

            var raster = anfrage.Raster switch
            {
                Auswertungsraster.Jahr => Zeitraster.Jahr,
                Auswertungsraster.Monat => Zeitraster.Monat,
                Auswertungsraster.Woche => Zeitraster.Woche,
                Auswertungsraster.Tag => Zeitraster.Tag,
                _ => throw new UngueltigeTourException("Auswertungsraster ist ungültig.")
            };
            var berechnet = BerechneAktivitaetsperioden.Ausfuehren(
                einheiten, anfrage.Von, anfrage.Bis, raster);

            ct.ThrowIfCancellationRequested();
            var perioden = berechnet.Select(p => new AktivitaetsperiodenAntwort(
                p.Von,
                p.Bis,
                p.Beschriftung,
                p.Anzahl,
                p.DistanzKm,
                p.Hoehenmeter,
                p.Fahrzeit.Ticks / TimeSpan.TicksPerSecond)).ToArray();
            var anzahl = perioden.Sum(p => p.Anzahl);
            var distanz = perioden.Sum(p => p.DistanzKm);
            var hoehenmeter = perioden.Aggregate(0L, (summe, p) => checked(summe + p.Hoehenmeter));
            var fahrzeit = perioden.Aggregate(0L, (summe, p) => checked(summe + p.FahrzeitSekunden));
            var zukuenftig = einheiten.Count(t => t.Datum > DateOnly.FromDateTime(DateTime.Today));

            return Task.FromResult(new AktivitaetsauswertungAntwort(
                anfrage.Von, anfrage.Bis, anfrage.Raster, anzahl, distanz,
                hoehenmeter, fahrzeit, zukuenftig, perioden));
        }
        catch (ArgumentException ex)
        {
            throw new UngueltigeTourException(ex.Message);
        }
        catch (OverflowException)
        {
            throw new UngueltigeTourException("Die Summen sind zu groß; bitte einen kürzeren Zeitraum wählen.");
        }
    }
}
