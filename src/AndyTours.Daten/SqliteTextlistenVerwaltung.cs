using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Versionierte, fachlich eingeschränkte Verwaltung der unterstützten Textlisten.</summary>
public sealed class SqliteTextlistenVerwaltung(string datenbankpfad) : ITextlistenVerwaltung
{
    private sealed record Snapshot(Auswahlliste Liste, bool Gespeichert, List<string> Eintraege, string Version);
    private SqliteConnection Oeffnen() => OeffneDatenbank.Ausfuehren(datenbankpfad);

    public Task<TextlisteAntwort> Laden(Textlistenart art, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var c = Oeffnen();
        return Task.FromResult(Antwort(Lesen(c, art)));
    }

    public Task<TextlisteAntwort> Anlegen(Textlistenart art, TextlisteneintragAnfrage anfrage, CancellationToken ct = default)
    {
        if (anfrage is null) throw Invalid();
        var name = ValidateName(anfrage.Name);
        return Mutate(art, anfrage.Version, (c, tx, s) =>
        {
            if (s.Eintraege.Any(x => VergleicheNamen.Ausfuehren(x, name))) throw Invalid("Der Eintrag ist bereits vorhanden.");
            s.Eintraege.Add(name);
            AuswahllisteEinstellung.Speichern(c, tx, s.Liste, s.Eintraege);
        }, ct);
    }

    public Task<TextlisteAntwort> Umbenennen(Textlistenart art, TextlisteUmbenennenAnfrage anfrage, CancellationToken ct = default)
    {
        if (anfrage is null) throw Invalid();
        var oldName = ValidateName(anfrage.AlterName); var newName = ValidateName(anfrage.NeuerName);
        return Mutate(art, anfrage.Version, (c, tx, s) =>
        {
            var index = s.Eintraege.FindIndex(x => VergleicheNamen.Ausfuehren(x, oldName));
            if (index < 0) throw NotFound();
            if (s.Eintraege.Where((_, i) => i != index).Any(x => VergleicheNamen.Ausfuehren(x, newName))) throw Invalid("Der Zielname ist bereits vorhanden.");
            var canonicalOld = s.Eintraege[index];
            if (!VergleicheNamen.Ausfuehren(canonicalOld, newName)) CascadeRename(c, tx, s.Liste, canonicalOld, newName);
            s.Eintraege[index] = newName;
            AuswahllisteEinstellung.Speichern(c, tx, s.Liste, s.Eintraege);
        }, ct);
    }

    public Task<TextlisteAntwort> Loeschen(Textlistenart art, TextlisteneintragAnfrage anfrage, CancellationToken ct = default)
    {
        if (anfrage is null) throw Invalid();
        var name = ValidateName(anfrage.Name);
        return Mutate(art, anfrage.Version, (c, tx, s) =>
        {
            var index = s.Eintraege.FindIndex(x => VergleicheNamen.Ausfuehren(x, name));
            if (index < 0) throw NotFound();
            if (IstVerwendet(c, tx, s.Liste, s.Eintraege[index]))
                throw new StammdatenException(StammdatenFehlerart.Konflikt, "Der Eintrag wird noch verwendet und kann nicht gelöscht werden.");
            s.Eintraege.RemoveAt(index);
            AuswahllisteEinstellung.Speichern(c, tx, s.Liste, s.Eintraege);
        }, ct);
    }

    public Task<TextlisteAntwort> SetzeReihenfolge(Textlistenart art, TextlisteReihenfolgeAnfrage anfrage, CancellationToken ct = default)
    {
        if (anfrage is null || anfrage.Eintraege is null) throw Invalid();
        var names = anfrage.Eintraege.Select(ValidateName).ToList();
        return Mutate(art, anfrage.Version, (c, tx, s) =>
        {
            if (names.Count != s.Eintraege.Count || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
                throw Invalid("Die Reihenfolge muss jeden Listeneintrag genau einmal enthalten.");
            var canonical = new List<string>(names.Count);
            foreach (var name in names)
            {
                var entry = s.Eintraege.SingleOrDefault(x => VergleicheNamen.Ausfuehren(x, name));
                if (entry is null || canonical.Any(x => VergleicheNamen.Ausfuehren(x, entry)))
                    throw Invalid("Die Reihenfolge muss eine vollständige Permutation der Liste sein.");
                canonical.Add(entry);
            }
            AuswahllisteEinstellung.Speichern(c, tx, s.Liste, canonical);
        }, ct);
    }

    private Task<TextlisteAntwort> Mutate(Textlistenart art, string? version,
        Action<SqliteConnection, SqliteTransaction, Snapshot> mutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ValidateVersion(version);
        using var c = Oeffnen(); using var tx = c.BeginTransaction(deferred: false);
        var snapshot = Lesen(c, art);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(snapshot.Version), Convert.FromHexString(version!)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Die Textliste wurde zwischenzeitlich geändert. Bitte neu laden.");
        mutation(c, tx, snapshot);
        ct.ThrowIfCancellationRequested();
        var updated = Lesen(c, art);
        ct.ThrowIfCancellationRequested(); tx.Commit();
        return Task.FromResult(Antwort(updated));
    }

    private static Snapshot Lesen(SqliteConnection c, Textlistenart art)
    {
        var list = Map(art);
        var stored = AuswahllisteEinstellung.Laden(c, list);
        var entries = stored ?? (list switch
        {
            Auswahlliste.Trainingsbereich => ErmittleNamensliste.Ausfuehren(DatenbestandTabellen.Laden(c).Trainingseinheiten.Select(x => x.Trainingsbereich)),
            Auswahlliste.Taetigkeit => ErmittleNamensliste.Ausfuehren(DatenbestandTabellen.Laden(c).Wartungen.Select(x => x.Taetigkeit)),
            _ => throw Invalid()
        });
        var material = JsonSerializer.SerializeToUtf8Bytes(new { Art = art, Gespeichert = stored is not null, Eintraege = entries });
        return new Snapshot(list, stored is not null, entries, Convert.ToHexString(SHA256.HashData(material)));
    }

    private static TextlisteAntwort Antwort(Snapshot s) => new(s.Liste switch
    {
        Auswahlliste.Trainingsbereich => Textlistenart.Trainingsbereich,
        Auswahlliste.Taetigkeit => Textlistenart.Taetigkeit,
        _ => throw Invalid()
    }, s.Version, s.Eintraege.ToArray());

    private static Auswahlliste Map(Textlistenart art) => art switch
    {
        Textlistenart.Trainingsbereich => Auswahlliste.Trainingsbereich,
        Textlistenart.Taetigkeit => Auswahlliste.Taetigkeit,
        _ => throw Invalid("Nicht unterstützte Textliste.")
    };

    private static void CascadeRename(SqliteConnection c, SqliteTransaction tx, Auswahlliste list, string oldName, string newName)
    {
        if (list == Auswahlliste.Trainingsbereich) TrainingseinheitenTabelle.ErsetzeTrainingsbereich(c, tx, oldName, newName);
        else WartungenTabelle.ErsetzeTaetigkeit(c, tx, oldName, newName);
    }

    private static bool IstVerwendet(SqliteConnection c, SqliteTransaction tx, Auswahlliste list, string name)
    {
        var data = DatenbestandTabellen.Laden(c);
        return list switch
        {
            Auswahlliste.Trainingsbereich => data.Trainingseinheiten.Any(x => VergleicheNamen.Ausfuehren(x.Trainingsbereich, name)),
            Auswahlliste.Taetigkeit => data.Wartungen.Any(x => VergleicheNamen.Ausfuehren(x.Taetigkeit, name)),
            _ => throw Invalid()
        };
    }

    private static string ValidateName(string? name)
    {
        if (name is null) throw Invalid("Name ist erforderlich.");
        var clean = name.Trim();
        if (clean.Length is < 1 or > 500 || clean.Any(char.IsControl)) throw Invalid("Name muss 1 bis 500 Zeichen ohne Steuerzeichen enthalten.");
        return clean;
    }

    private static void ValidateVersion(string? version)
    { if (version is null || version.Length != 64 || !version.All(Uri.IsHexDigit)) throw Invalid("Versionskennung ist ungültig."); }
    private static StammdatenException Invalid(string message = "Textlistenanforderung ist ungültig.") => new(StammdatenFehlerart.Ungueltig, message);
    private static StammdatenException NotFound() => new(StammdatenFehlerart.NichtGefunden, "Listeneintrag wurde nicht gefunden.");
}
