using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;

namespace AndyTours.Client;

public sealed partial class TourenMehrfachHttpClient : ITourenMehrfachverwaltung
{
    public Task<TourenMehrfachAntwort> Kategorien(TourenKategorienAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = TourenMehrfachRegeln.Normalisieren(anfrage);
        return Senden("kategorien", request, request.AnfrageId, request.Touren, false, cancellationToken);
    }

    public Task<TourenMehrfachAntwort> Aktivitaet(TourenAktivitaetAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = TourenMehrfachRegeln.Normalisieren(anfrage);
        return Senden("aktivitaet", request, request.AnfrageId, request.Touren, false, cancellationToken);
    }

    public Task<TourenMehrfachAntwort> Equipment(TourenEquipmentAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = TourenMehrfachRegeln.Normalisieren(anfrage);
        return Senden("equipment", request, request.AnfrageId, request.Touren, false, cancellationToken);
    }

    public Task<TourenMehrfachAntwort> Loeschen(TourenLoeschenAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = TourenMehrfachRegeln.Normalisieren(anfrage);
        return Senden("loeschen", request, request.AnfrageId, request.Touren, true, cancellationToken);
    }
}
