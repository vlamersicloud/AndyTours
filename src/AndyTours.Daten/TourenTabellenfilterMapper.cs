using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using AndyTours.Core.Berechnungen;

namespace AndyTours.Daten;

/// <summary>Ordnet additive Tabellenfilter unverändert den bestehenden Core-Filterregeln zu.</summary>
public static class TourenTabellenfilterMapper
{
    public static Einheitenfilter ToFilter(TourenTabellenfilter? raw)
    {
        if (raw is null)
            return Einheitenfilter.Leer;

        return new Einheitenfilter(
            Kategorie: null,
            Equipment: raw.EquipmentName,
            Trainingsbereich: raw.Trainingsbereich,
            Distanz: LeseFilterbereich.Ausfuehren(raw.DistanzVon, raw.DistanzBis),
            Hoehenmeter: LeseFilterbereich.Ausfuehren(raw.HoehenmeterVon, raw.HoehenmeterBis),
            Suchbegriff: raw.Suchbegriff ?? string.Empty);
    }
}