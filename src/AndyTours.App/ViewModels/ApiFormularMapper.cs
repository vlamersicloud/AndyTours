using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;

namespace AndyTours.App.ViewModels;

/// <summary>
/// Übersetzt HTTP-Transportdaten in die Core-Modelle, die bestehende WPF-Formulare erwarten.
/// Dieser reine Darstellungsadapter enthält keine Persistenz-, Datei-, Import- oder Backendlogik.
/// </summary>
public static class ApiFormularMapper
{
    /// <summary>Erstellt aus einer gelesenen API-Tour das Modell für das bestehende Einheit-Formular.</summary>
    /// <remarks>Anhänge und Dateipfade werden absichtlich nicht übernommen; die API-Antwort enthält sie nicht.</remarks>
    public static Trainingseinheit AlsEinheit(TourAntwort antwort)
    {
        ArgumentNullException.ThrowIfNull(antwort);
        ArgumentNullException.ThrowIfNull(antwort.Werte);
        var werte = antwort.Werte;

        return new Trainingseinheit
        {
            Id = antwort.Id,
            FahrerId = werte.FahrerId,
            Datum = werte.Datum,
            Startzeit = werte.Startzeit,
            Name = werte.Name,
            Fahrzeit = TimeSpan.FromTicks(checked(werte.FahrzeitSekunden * TimeSpan.TicksPerSecond)),
            DistanzKm = werte.DistanzKm,
            Hoehenmeter = werte.Hoehenmeter,
            MaxHoehe = werte.MaxHoehe,
            StartHoehe = werte.StartHoehe,
            MaxGeschwindigkeit = werte.MaxGeschwindigkeit,
            HfDurchschnitt = werte.HfDurchschnitt,
            HfMax = werte.HfMax,
            HfMin = werte.HfMin,
            Trittfrequenz = werte.Trittfrequenz,
            Temperatur = werte.Temperatur,
            LeistungWatt = werte.LeistungWatt,
            KoerpergewichtKg = werte.KoerpergewichtKg,
            Erholungspuls = werte.Erholungspuls,
            Trainingsbereich = werte.Trainingsbereich,
            Kommentar = werte.Kommentar,
            AktivitaetId = werte.AktivitaetId,
            EquipmentId = werte.EquipmentId,
            KategorieIds = [.. werte.KategorieIds]
        };
    }

    /// <summary>Erstellt aus einer Formular-Trainingseinheit das explizite API-Transportobjekt.</summary>
    public static TourWerte AlsWerte(Trainingseinheit einheit)
    {
        ArgumentNullException.ThrowIfNull(einheit);

        return new TourWerte(
            FahrerId: einheit.FahrerId,
            Datum: einheit.Datum,
            Startzeit: einheit.Startzeit,
            Name: einheit.Name,
            FahrzeitSekunden: einheit.Fahrzeit.Ticks / TimeSpan.TicksPerSecond,
            DistanzKm: einheit.DistanzKm,
            Hoehenmeter: einheit.Hoehenmeter,
            MaxHoehe: einheit.MaxHoehe,
            StartHoehe: einheit.StartHoehe,
            MaxGeschwindigkeit: einheit.MaxGeschwindigkeit,
            HfDurchschnitt: einheit.HfDurchschnitt,
            HfMax: einheit.HfMax,
            HfMin: einheit.HfMin,
            Trittfrequenz: einheit.Trittfrequenz,
            Temperatur: einheit.Temperatur,
            LeistungWatt: einheit.LeistungWatt,
            KoerpergewichtKg: einheit.KoerpergewichtKg,
            Erholungspuls: einheit.Erholungspuls,
            Trainingsbereich: einheit.Trainingsbereich,
            Kommentar: einheit.Kommentar,
            AktivitaetId: einheit.AktivitaetId,
            EquipmentId: einheit.EquipmentId,
            KategorieIds: [.. einheit.KategorieIds]);
    }

    /// <summary>Bildet die vom Backend gelieferten Stammdaten für bestehende Formulare ab.</summary>
    /// <remarks>Nur die im Vertrag enthaltenen vier Sammlungen werden gesetzt; weitere Bestandslisten bleiben leer.</remarks>
    public static Datenbestand AlsStammdaten(StammdatenAntwort antwort)
    {
        ArgumentNullException.ThrowIfNull(antwort);

        return new Datenbestand
        {
            Fahrer = antwort.Fahrer.Select(f => new Fahrer
            {
                Id = f.Id,
                Vorname = f.Vorname,
                Nachname = f.Nachname,
                Geburtstag = f.Geburtstag,
                GroesseCm = f.GroesseCm,
                GewichtKg = f.GewichtKg,
                Ruhepuls = f.Ruhepuls,
                Maxpuls = f.Maxpuls,
                Heimatort = f.Heimatort
            }).ToList(),
            Aktivitaeten = antwort.Aktivitaeten.Select(a => new Aktivitaet
            {
                Id = a.Id,
                Name = a.Name,
                IstHaupt = a.IstHaupt,
                Geschwindigkeitseinheit = a.Geschwindigkeitseinheit,
                ZielKm = a.ZielKm,
                ZielHm = a.ZielHm,
                ZielAnzahl = a.ZielAnzahl
            }).ToList(),
            Kategorien = antwort.Kategorien.Select(k => new Kategorie
            {
                Id = k.Id,
                Name = k.Name,
                IstStandard = k.IstStandard
            }).ToList(),
            Equipments = antwort.Equipments.Select(e => new Equipment
            {
                Id = e.Id,
                Name = e.Name,
                AktivitaetId = e.AktivitaetId,
                Geraetezuordnung = e.Geraetezuordnung,
                StandardKategorie = e.StandardKategorie,
                IstStandard = e.IstStandard,
                Ausgemustert = e.Ausgemustert,
                Anschaffungsdatum = e.Anschaffungsdatum,
                AnfangsdistanzKm = e.AnfangsdistanzKm
            }).ToList()
        };
    }
}
