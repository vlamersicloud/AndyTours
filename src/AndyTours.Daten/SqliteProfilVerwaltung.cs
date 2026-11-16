using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Versionierte atomare Verwaltung des persönlichen Profils.</summary>
public sealed class SqliteProfilVerwaltung(string datenbankpfad) : IProfilVerwaltung
{
    private SqliteConnection Oeffnen() => OeffneDatenbank.Ausfuehren(datenbankpfad);

    public Task<ProfilAntwort> Laden(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var c = Oeffnen(); using var tx = c.BeginTransaction(deferred: true);
        var result = Antwort(c); ct.ThrowIfCancellationRequested(); tx.Commit(); return Task.FromResult(result);
    }

    public Task<ProfilAntwort> Speichern(ProfilSpeichernAnfrage anfrage, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (anfrage is null || anfrage.Werte is null) throw Invalid();
        ValidateVersionInput(anfrage.Version);
        Validate(anfrage.Werte);
        using var c = Oeffnen(); using var tx = c.BeginTransaction(deferred: false);
        var current = Antwort(c);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(current.Version), Convert.FromHexString(anfrage.Version)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Persönliches Profil wurde zwischenzeitlich geändert. Bitte neu laden.");
        var values = anfrage.Werte;
        var driver = new Fahrer { Vorname = values.Vorname.Trim(), Nachname = values.Nachname.Trim(), Geburtstag = values.Geburtstag,
            GroesseCm = values.GroesseCm, GewichtKg = values.GewichtKg, Ruhepuls = values.Ruhepuls, Maxpuls = values.Maxpuls, Heimatort = values.Heimatort.Trim() };
        FahrerTabelle.SpeicherePersoenlicheDaten(c, tx, driver);
        var result = Antwort(c); ct.ThrowIfCancellationRequested(); tx.Commit(); return Task.FromResult(result);
    }

    private static ProfilAntwort Antwort(SqliteConnection c)
    {
        var driver = FahrerTabelle.Laden(c).OrderBy(f => f.Id).FirstOrDefault();
        var dto = driver is null ? null : new FahrerAntwort(driver.Id, driver.Vorname, driver.Nachname, driver.Geburtstag,
            driver.GroesseCm, driver.GewichtKg, driver.Ruhepuls, driver.Maxpuls, driver.Heimatort);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(dto)));
        return new ProfilAntwort(hash, dto);
    }

    private static void ValidateVersionInput(string? version)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Length != 64 || !version.All(Uri.IsHexDigit))
            throw Invalid("Versionskennung fehlt oder ist ungültig.");
    }

    private static void Validate(ProfilWerte w)
    {
        CheckText(w.Vorname, "Vorname"); CheckText(w.Nachname, "Nachname"); CheckText(w.Heimatort, "Heimatort");
        if (w.Geburtstag > DateOnly.FromDateTime(DateTime.UtcNow)) throw Invalid("Geburtstag darf nicht in der Zukunft liegen.");
        if (w.GroesseCm != 0 && w.GroesseCm is < 100 or > 250) throw Invalid("Größe muss 0 (unbekannt) oder 100 bis 250 cm betragen.");
        if (!double.IsFinite(w.GewichtKg) || (w.GewichtKg != 0 && (w.GewichtKg < 30 || w.GewichtKg > 250 || Math.Abs(Math.Round(w.GewichtKg, 1) - w.GewichtKg) > 1e-9)))
            throw Invalid("Gewicht muss 0 (unbekannt) oder 30 bis 250 kg mit höchstens einer Nachkommastelle betragen.");
        if (w.Ruhepuls != 0 && w.Ruhepuls is < 30 or > 120) throw Invalid("Ruhepuls muss 0 (unbekannt) oder 30 bis 120 betragen.");
        if (w.Maxpuls != 0 && w.Maxpuls is < 100 or > 230) throw Invalid("Maximalpuls muss 0 (unbekannt) oder 100 bis 230 betragen.");
        if (w.Ruhepuls > 0 && w.Maxpuls > 0 && w.Ruhepuls >= w.Maxpuls) throw Invalid("Ruhepuls muss kleiner als Maximalpuls sein.");
    }

    private static void CheckText(string? value, string field)
    {
        if (value is null || value.Trim().Length > 500 || value.Any(char.IsControl)) throw Invalid($"{field} muss höchstens 500 Zeichen enthalten und darf keine Steuerzeichen enthalten.");
    }
    private static StammdatenException Invalid(string message = "Profilanforderung ist ungültig.") => new(StammdatenFehlerart.Ungueltig, message);
}
