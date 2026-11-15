using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;

namespace AndyTours.Daten;

/// <summary>Liest eine begrenzte Stammdatenauswahl aus einer eigenen SQLite-Snapshotverbindung.</summary>
public sealed class SqliteStammdatenLeser(string datenbankpfad) : IStammdatenLeser
{
    /// <inheritdoc />
    public Task<StammdatenAntwort> Laden(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        using var verbindung = OeffneDatenbank.Ausfuehren(datenbankpfad);
        using var transaktion = verbindung.BeginTransaction(deferred: true);

        var fahrer = FahrerTabelle.Laden(verbindung)
            .Select(f => new FahrerAntwort(
                f.Id, f.Vorname, f.Nachname, f.Geburtstag, f.GroesseCm,
                f.GewichtKg, f.Ruhepuls, f.Maxpuls, f.Heimatort))
            .ToArray();

        var aktivitaeten = OrdneAktivitaeten.Ausfuehren(
                AktivitaetenTabelle.Laden(verbindung),
                AktivitaetenReihenfolgeEinstellung.Laden(verbindung))
            .Select(a => new AktivitaetAntwort(
                a.Id, a.Name, a.IstHaupt, a.Geschwindigkeitseinheit,
                a.ZielKm, a.ZielHm, a.ZielAnzahl))
            .ToArray();

        var kategorien = KategorienTabelle.Laden(verbindung)
            .Select(k => new KategorieAntwort(k.Id, k.Name, k.IstStandard))
            .ToArray();

        var equipments = EquipmentsTabelle.Laden(verbindung)
            .Select(e => new EquipmentAntwort(
                e.Id, e.Name, e.AktivitaetId, e.Geraetezuordnung, e.StandardKategorie,
                e.IstStandard, e.Ausgemustert, e.Anschaffungsdatum, e.AnfangsdistanzKm))
            .ToArray();

        IReadOnlyList<string> trainingsbereiche;
        var gespeicherteListe = AuswahllisteEinstellung.Laden(verbindung, Auswahlliste.Trainingsbereich);
        if (gespeicherteListe is not null)
        {
            trainingsbereiche = gespeicherteListe;
        }
        else
        {
            trainingsbereiche = ErmittleNamensliste.Ausfuehren(
                TrainingseinheitenTabelle.Laden(verbindung).Select(t => t.Trainingsbereich));
        }

        ct.ThrowIfCancellationRequested();
        transaktion.Commit();

        return Task.FromResult(new StammdatenAntwort(
            fahrer, aktivitaeten, kategorien, equipments, trainingsbereiche));
    }
}
