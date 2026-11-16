using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Versionierte SQLite-Verwaltung saisonaler Ziele.</summary>
public sealed class SqliteSaisonzieleVerwaltung(string datenbankpfad) : ISaisonzieleVerwaltung
{
    private SqliteConnection Oeffnen() => OeffneDatenbank.Ausfuehren(datenbankpfad);

    public Task<SaisonzieleAntwort> Laden(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var c = Oeffnen(); using var tx = c.BeginTransaction(deferred: true);
        var result = Read(c);
        ct.ThrowIfCancellationRequested(); tx.Commit();
        return Task.FromResult(result);
    }

    public Task<SaisonzieleAntwort> Speichern(SaisonzieleSpeichernAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Validate(anfrage);
        using var c = Oeffnen(); using var tx = c.BeginTransaction(deferred: false);
        var current = Read(c);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(current.Version), Convert.FromHexString(anfrage.Version)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Saisonziele oder Aktivitäten wurden zwischenzeitlich geändert. Bitte neu laden.");
        var activityIds = current.Aktivitaeten.Select(a => a.Id).ToHashSet();
        foreach (var goal in anfrage.Ziele)
        {
            if (!activityIds.Contains(goal.AktivitaetId))
                throw new StammdatenException(StammdatenFehlerart.NichtGefunden, "Aktivität für ein Saisonziel wurde nicht gefunden.");
        }
        ct.ThrowIfCancellationRequested();
        var goals = anfrage.Ziele.Select(g => new Saisonziel
        {
            AktivitaetId = g.AktivitaetId, AbJahr = g.AbJahr, ZielKm = g.ZielKm, ZielHm = g.ZielHm, ZielAnzahl = g.ZielAnzahl
        }).ToArray();
        SaisonzieleTabelle.SpeichereJahre(c, tx, anfrage.Jahre, goals);
        ct.ThrowIfCancellationRequested();
        var response = Read(c);
        ct.ThrowIfCancellationRequested(); tx.Commit();
        return Task.FromResult(response);
    }

    private static SaisonzieleAntwort Read(SqliteConnection c)
    {
        var activities = AktivitaetenTabelle.Laden(c).Select(a => new AktivitaetAntwort(a.Id, a.Name, a.IstHaupt,
            a.Geschwindigkeitseinheit, a.ZielKm, a.ZielHm, a.ZielAnzahl)).ToArray();
        var goals = SaisonzieleTabelle.Laden(c).OrderBy(g => g.AktivitaetId).ThenBy(g => g.AbJahr)
            .Select(g => new SaisonzielAntwort(g.AktivitaetId, g.AbJahr, g.ZielKm, g.ZielHm, g.ZielAnzahl)).ToArray();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { Aktivitaeten = activities, Ziele = goals });
        return new SaisonzieleAntwort(Convert.ToHexString(SHA256.HashData(bytes)), activities, goals);
    }

    private static void Validate(SaisonzieleSpeichernAnfrage? request)
    {
        if (request is null || request.Version is null || request.Version.Length != 64 || !request.Version.All(Uri.IsHexDigit) ||
            request.Jahre is null || request.Ziele is null || request.Jahre.Count is < 1 or > 20 ||
            request.Jahre.Any(y => y is < 1 or > 9999) || request.Jahre.Distinct().Count() != request.Jahre.Count)
            throw Invalid();
        var years = request.Jahre.ToHashSet();
        if (request.Ziele.Any(g => g is null || g.AbJahr is < 1 or > 9999 || !years.Contains(g.AbJahr) ||
                                   g.AktivitaetId <= 0 || g.ZielKm < 0 || g.ZielHm < 0 || g.ZielAnzahl < 0) ||
            request.Ziele.Select(g => (g.AktivitaetId, g.AbJahr)).Distinct().Count() != request.Ziele.Count)
            throw Invalid("Saisonziele müssen gültige, eindeutige Aktivitäts-/Jahrpaare innerhalb der ausgewählten Jahre sein.");
    }

    private static StammdatenException Invalid(string message = "Saisonzielanforderung ist ungültig.") =>
        new(StammdatenFehlerart.Ungueltig, message);
}
