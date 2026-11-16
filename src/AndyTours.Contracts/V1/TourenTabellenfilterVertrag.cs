namespace AndyTours.Contracts.V1;

/// <summary>
/// Optionale zusätzliche Filter ausschließlich für die Tourentabelle; diese Werte sind Anzeigetexte,
/// keine Ressourcenkennungen und keine Änderungszuordnungen.
/// </summary>
/// <param name="EquipmentName">Name gemäß Core-Equipmentfilter; passende Namen können mehrere Equipment-IDs treffen.</param>
/// <param name="Trainingsbereich">Exaktes Trainingsbereichskürzel.</param>
/// <param name="DistanzVon">Unveränderter Texteingabewert der inklusiven unteren Distanzgrenze.</param>
/// <param name="DistanzBis">Unveränderter Texteingabewert der inklusiven oberen Distanzgrenze.</param>
/// <param name="HoehenmeterVon">Unveränderter Texteingabewert der inklusiven unteren Höhenmetergrenze.</param>
/// <param name="HoehenmeterBis">Unveränderter Texteingabewert der inklusiven oberen Höhenmetergrenze.</param>
/// <param name="Suchbegriff">Wortsuche über Tourname und Kommentar.</param>
/// <remarks>
/// Bei EquipmentName und Trainingsbereich bedeutet null „alle“. Leere Zahlengrenzen bedeuten keine Grenze;
/// Zahlenbereichstexte werden unverändert durch die gemeinsame Core-Eingabeauswertung interpretiert (Komma und
/// Punkt als Dezimalzeichen); fehlerhafte Bereiche sind inaktiv. Leere oder nur aus Leerzeichen bestehende Suche
/// enthält keine Wörter. Sonst muss jedes Suchwort in Name oder Kommentar vorkommen (OrdinalIgnoreCase).
/// Der Equipmentname wird gemäß Core-Regel sowohl mit als auch ohne Ausmusterungszusatz geprüft; mehrere IDs
/// mit passendem Namen bleiben Treffer, es wird keine Identität aus Anzeigetext abgeleitet.
/// Diese Tabellenfilter beeinflussen weder Statistik noch Rekorde oder Jahresziele.
/// </remarks>
public sealed record TourenTabellenfilter(
    string? EquipmentName = null,
    string? Trainingsbereich = null,
    string? DistanzVon = null,
    string? DistanzBis = null,
    string? HoehenmeterVon = null,
    string? HoehenmeterBis = null,
    string? Suchbegriff = null);
