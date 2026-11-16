using AndyTours.Application.Auswertung;
using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;

namespace AndyTours.Daten;

/// <summary>Erzeugt aggregierte Aktivitätsauswertungen aus einem konsistenten SQLite-Lesesnapshot.</summary>
public sealed class SqliteAktivitaetsauswertung(string datenbankpfad) : IAktivitaetsauswertung
{
    public Task<AktivitaetsauswertungAntwort> Laden(AktivitaetsauswertungAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (anfrage is null || anfrage.Von > anfrage.Bis || !Enum.IsDefined(anfrage.Raster) ||
            anfrage.AktivitaetId is <= 0 || anfrage.KategorieId is <= 0)
            throw new UngueltigeTourException("Filter oder Zeitraum der Auswertung sind ungültig.");
        try
        {
            using var verbindung = OeffneDatenbank.Ausfuehren(datenbankpfad);
            using var transaktion = verbindung.BeginTransaction(deferred: true);
            var alleEinheiten = TrainingseinheitenTabelle.Laden(verbindung);
            var aktivitaeten = AktivitaetenTabelle.Laden(verbindung);
            var saisonziele = SaisonzieleTabelle.Laden(verbindung);
            var einheiten = alleEinheiten
                .Where(t => !anfrage.AktivitaetId.HasValue || t.AktivitaetId == anfrage.AktivitaetId.Value)
                .Where(t => !anfrage.KategorieId.HasValue || t.KategorieIds.Contains(anfrage.KategorieId.Value)).ToList();
            var raster = anfrage.Raster switch
            {
                Auswertungsraster.Jahr => Zeitraster.Jahr,
                Auswertungsraster.Monat => Zeitraster.Monat,
                Auswertungsraster.Woche => Zeitraster.Woche,
                Auswertungsraster.Tag => Zeitraster.Tag,
                _ => throw new UngueltigeTourException("Auswertungsraster ist ungültig.")
            };
            var berechnet = BerechneAktivitaetsperioden.Ausfuehren(einheiten, anfrage.Von, anfrage.Bis, raster);
            ct.ThrowIfCancellationRequested();
            var perioden = berechnet.Select(p => new AktivitaetsperiodenAntwort(p.Von, p.Bis, p.Beschriftung, p.Anzahl,
                p.DistanzKm, p.Hoehenmeter, p.Fahrzeit.Ticks / TimeSpan.TicksPerSecond)).ToArray();
            var anzahl = perioden.Sum(p => p.Anzahl);
            var distanz = perioden.Sum(p => p.DistanzKm);
            var hoehenmeter = perioden.Aggregate(0L, (summe, p) => checked(summe + p.Hoehenmeter));
            var fahrzeit = perioden.Aggregate(0L, (summe, p) => checked(summe + p.FahrzeitSekunden));
            var heute = DateOnly.FromDateTime(DateTime.Today);
            var zukuenftig = einheiten.Count(t => t.Datum > heute);
            var tourenImZeitraum = einheiten.Where(t => t.Datum >= anfrage.Von && t.Datum <= anfrage.Bis).ToList();
            var projektion = new Datenbestand { Trainingseinheiten = tourenImZeitraum, Aktivitaeten = aktivitaeten, Saisonziele = saisonziele };
            var bestleistungen = ErmittleBestleistungen.Ausfuehren(projektion, null, null);
            var beschrieben = BeschreibeBestleistungen.Ausfuehren(bestleistungen, heute);
            var jahresrekorde = beschrieben.Jahresrekorde.Select(x => new BestleistungszeileAntwort(x.Bezeichnung, x.Wert, x.Herkunft, x.IstLaufendesJahr, x.EinheitId)).ToArray();
            var einzelrekorde = beschrieben.Einzelrekorde.Select(x => new BestleistungszeileAntwort(x.Bezeichnung, x.Wert, x.Herkunft, x.IstLaufendesJahr, x.EinheitId)).ToArray();
            var zielAktivitaeten = anfrage.AktivitaetId is { } id ? aktivitaeten.Where(a => a.Id == id).ToList() : aktivitaeten;
            var jahresziele = Enumerable.Range(anfrage.Von.Year, checked(anfrage.Bis.Year - anfrage.Von.Year + 1)).Select(jahr =>
            {
                long km = 0, hm = 0, anzahlZiele = 0;
                foreach (var aktivitaet in zielAktivitaeten)
                {
                    var ziel = ErmittleSaisonziel.Ausfuehren(aktivitaet, saisonziele, jahr);
                    km = checked(km + (long)ziel.ZielKm);
                    hm = checked(hm + (long)ziel.ZielHm);
                    anzahlZiele = checked(anzahlZiele + ziel.ZielAnzahl);
                }
                return new JahreszielAntwort(jahr, km, hm, anzahlZiele);
            }).ToArray();
            ct.ThrowIfCancellationRequested();
            transaktion.Commit();
            return Task.FromResult(new AktivitaetsauswertungAntwort(anfrage.Von, anfrage.Bis, anfrage.Raster, anzahl, distanz,
                hoehenmeter, fahrzeit, zukuenftig, perioden)
            {
                Jahresrekorde = jahresrekorde,
                Einzelrekorde = einzelrekorde,
                Jahresziele = jahresziele
            });
        }
        catch (ArgumentException ex) { throw new UngueltigeTourException(ex.Message); }
        catch (OverflowException) { throw new UngueltigeTourException("Die Summen sind zu groß; bitte einen kürzeren Zeitraum wählen."); }
    }
}
