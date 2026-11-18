using AndyTours.Contracts.V1;

namespace AndyTours.Application.Touren;

/// <summary>HTTP-Port für die explizite, begrenzte Auswahl aller angewandten Filtertreffer.</summary>
public interface ITourenFilterauswahlClient
{
    Task<TourenFilterauswahlAntwort> Auswaehlen(TourenFilterauswahlAnfrage filter, CancellationToken ct = default);
}
