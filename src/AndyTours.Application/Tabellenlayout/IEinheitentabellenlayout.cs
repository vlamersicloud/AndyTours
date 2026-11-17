using AndyTours.Contracts.V1;

namespace AndyTours.Application.Tabellenlayout;

public interface IEinheitentabellenlayout
{
    Task<EinheitentabellenlayoutAntwort> Laden(CancellationToken cancellationToken);
    Task<EinheitentabellenlayoutAntwort> Speichern(EinheitentabellenlayoutAnfrage anfrage, CancellationToken cancellationToken);
}
