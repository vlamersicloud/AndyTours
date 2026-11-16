using AndyTours.Application.Auswertung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.Daten;

public sealed partial class SqlitePulsQuelldaten
{
    public Task<PulsQuellbestand> Laden(PulsAuswertungsAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (anfrage is null || anfrage.Von > anfrage.Bis || !Enum.IsDefined(anfrage.Raster) ||
            anfrage.AktivitaetId is <= 0 || anfrage.KategorieId is <= 0 ||
            (anfrage.ProfilSport is not null && string.IsNullOrWhiteSpace(anfrage.ProfilSport)))
            throw Ungueltig();

        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        cancellationToken.ThrowIfCancellationRequested();
        var all = TrainingseinheitenTabelle.Laden(db);
        cancellationToken.ThrowIfCancellationRequested();
        var selected = all
            .Where(tour => tour.Datum >= anfrage.Von && tour.Datum <= anfrage.Bis)
            .Where(tour => !anfrage.AktivitaetId.HasValue || tour.AktivitaetId == anfrage.AktivitaetId.Value)
            .Where(tour => !anfrage.KategorieId.HasValue || tour.KategorieIds.Contains(anfrage.KategorieId.Value))
            .ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        var reference = ReadReference(db, tx);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(new PulsQuellbestand(selected, reference.Version, reference.Stand));
    }
}
