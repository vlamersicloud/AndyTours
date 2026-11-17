namespace AndyTours.Contracts.V1;

/// <summary>Stabile öffentliche Schlüssel der 18 Tabellenspalten.</summary>
public enum Einheitentabellenschluessel
{
    Datum = 0, Startzeit = 1, Name = 2, Aktivitaet = 3, Kategorien = 4, Equipment = 5,
    Distanz = 6, Hoehenmeter = 7, Fahrzeit = 8, DurchschnittKmh = 9, MaxKmh = 10,
    PulsDurchschnitt = 11, PulsMax = 12, Trittfrequenz = 13, Temperatur = 14,
    MaxHoehe = 15, Trainingsbereich = 16, Kommentar = 17
}

/// <summary>Spalte mit optionaler Breite in WPF-DIPs bei 100 %; null bedeutet Auto, keine Prozentangabe.</summary>
public sealed record Einheitentabellenspalte(Einheitentabellenschluessel Schluessel, double? Breite);
/// <summary>Layout mit opaker Version des unveränderten serverseitigen Rohzustands.</summary>
public sealed record EinheitentabellenlayoutAntwort(string Version, IReadOnlyList<Einheitentabellenspalte> Anordnung, IReadOnlyList<Einheitentabellenschluessel> Ausgeblendet);
public sealed record EinheitentabellenlayoutAnfrage(string Version, IReadOnlyList<Einheitentabellenspalte> Anordnung, IReadOnlyList<Einheitentabellenschluessel> Ausgeblendet);
