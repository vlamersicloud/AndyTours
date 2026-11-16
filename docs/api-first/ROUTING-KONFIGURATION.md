# Entwurf: Routingkonfiguration im API-Modus

Status: Designvorschlag, nicht implementiert. Quellen: `src/AndyTours.Daten/RoutingEinstellung.cs` (drei bestehende Schlüssel: Dienst, BRouter-Adresse, ORS-Schlüssel), `src/AndyTours.App/ViewModels/EinstellungenViewModel.cs` und `EinstellungenViewModel.Routing.cs`; Garmin-Muster: `IGarminEinstellungen` und `GarminEinstellungenVertrag.cs`.

Empfehlung: sechs explizite Aktionen — `Laden`, `DienstSpeichern`, `AdresseSpeichern`, `AdresseZuruecksetzen`, `OrsKeySpeichern`, `OrsKeyEntfernen`. Ein gemeinsames CAS über die Rohwerte aller drei Routing-Schlüssel verhindert verlorene parallele Änderungen; jede Mutation schreibt nur ihr eigenes Feld und erhält die anderen unverändert.

GET liefert Dienst, sichere Adresse (oder verborgen/ungültig-Flag) und nur `HatSchluessel`, nie den Schlüssel. Eine Adresse darf normalisiert nur als HTTP(S)-URI ohne UserInfo, Query oder Fragment ausgegeben werden. Bereits gespeicherte nicht sichere URI bleibt verborgen und unverändert, bis der Nutzer sie ausdrücklich ersetzt oder zurücksetzt. Request-DTOs redigieren `ToString`; Schlüssel speichern/entfernen erfordert ausdrückliche Aktion. BRouter bleibt konfigurierbarer Provider, aber Clients wählen keine URL pro Route. Keine Providerabfrage beim Laden/Speichern der Konfiguration.

UI: unabhängiges API-Einstellungsfenster, ohne Legacy-SQL-ViewModel.