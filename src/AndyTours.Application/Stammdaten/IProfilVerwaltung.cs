using AndyTours.Contracts.V1;

namespace AndyTours.Application.Stammdaten;

/// <summary>Versionierter Port für das persönliche Profil.</summary>
public interface IProfilVerwaltung
{
    /// <summary>Lädt das Profil des ersten Fahrers; ein leerer Bestand bleibt leer.</summary>
    Task<ProfilAntwort> Laden(CancellationToken ct = default);
    /// <summary>Speichert das persönliche Profil atomar.</summary>
    Task<ProfilAntwort> Speichern(ProfilSpeichernAnfrage anfrage, CancellationToken ct = default);
}
