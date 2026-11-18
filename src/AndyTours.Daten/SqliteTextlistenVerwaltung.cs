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
    private SqliteConnection OeffnenNurLesen()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = datenbankpfad,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public Task<TextlistenNutzungAntwort> NutzungLesen(Textlistenart art, string listenVersion, string name, CancellationToken ct = default)
    {
        ValidateVersion(listenVersion);
        Map(art);
        var canonical = ValidateName(name);
        ct.ThrowIfCancellationRequested();
        using var connection = OeffnenNurLesen();
        using var tx = connection.BeginTransaction(deferred: true);
        var snapshot = Lesen(connection, art);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(snapshot.Version), Convert.FromHexString(listenVersion)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Die Textliste wurde zwischenzeitlich geändert. Bitte neu laden.");
        var currentName = snapshot.Eintraege.SingleOrDefault(x => VergleicheNamen.Ausfuehren(x, canonical));
        if (currentName is null) throw NotFound();
        var usage = ErmittleVerwendung(connection, tx, art, currentName, ct);
        ct.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(new TextlistenNutzungAntwort(art, snapshot.Version, currentName,
            usage.Touren, usage.Wartungen, usage.Alarme, usage.Version));
    }

    public Task<TextlisteAntwort> Laden(Textlistenart art, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Map(art);
        using var c = Oeffnen();
        using var tx = c.BeginTransaction(deferred: true);
        var answer = Antwort(Lesen(c, art));
        ct.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(answer);
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
            if (string.IsNullOrWhiteSpace(anfrage.VerwendungsVersion) || anfrage.VerwendungsVersion.Length != 64 ||
                !anfrage.VerwendungsVersion.All(Uri.IsHexDigit))
                throw Invalid("Eine gültige Nutzungsvorschau ist erforderlich.");
            var usage = ErmittleVerwendung(c, tx, art, canonicalOld, ct);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(usage.Version), Convert.FromHexString(anfrage.VerwendungsVersion)))
                throw new StammdatenException(StammdatenFehlerart.Konflikt, "Die Verwendung hat sich geändert. Bitte neu laden und erneut bestätigen.");
            if (!string.Equals(canonicalOld, newName, StringComparison.Ordinal))
                CascadeRename(c, tx, s.Liste, canonicalOld, newName, usage.Ids);
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
            if (IstVerwendet(c, tx, s.Liste, s.Eintraege[index], ct))
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
        Map(art);
        var snapshot = Lesen(c, art);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(snapshot.Version), Convert.FromHexString(version!)))
            throw new StammdatenException(StammdatenFehlerart.Konflikt, "Die Textliste wurde zwischenzeitlich geändert. Bitte neu laden.");
        ct.ThrowIfCancellationRequested();
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

    private static void CascadeRename(SqliteConnection c, SqliteTransaction tx, Auswahlliste list, string oldName, string newName, IReadOnlyList<long> usageIds)
    {
        if (list == Auswahlliste.Trainingsbereich)
        {
            using var command = c.CreateCommand();
            command.Transaction = tx;
            command.CommandText = "UPDATE Trainingseinheit SET Trainingsbereich = $new WHERE Id = $id;";
            var id = command.Parameters.Add("$id", SqliteType.Integer);
            command.Parameters.AddWithValue("$new", newName.Trim());
            foreach (var usageId in usageIds)
            {
                id.Value = usageId;
                if (command.ExecuteNonQuery() != 1)
                    throw new StammdatenException(StammdatenFehlerart.Konflikt, "Die betroffenen Touren haben sich geändert. Bitte neu laden.");
            }
        }
        else
        {
            WartungenTabelle.ErsetzeTaetigkeit(c, tx, oldName, newName);
        }
    }

    private sealed record Verwendungszeile(long Id, string Wert, string Art);
    private sealed record Verwendung(int Touren, int Wartungen, int Alarme, string Version, IReadOnlyList<long> Ids);

    private static Verwendung ErmittleVerwendung(SqliteConnection c, SqliteTransaction? tx, Textlistenart art, string name, CancellationToken ct)
    {
        var rows = new List<Verwendungszeile>();
        if (art == Textlistenart.Trainingsbereich)
        {
            using var command = c.CreateCommand();
            command.Transaction = tx;
            command.CommandText = "SELECT Id, Trainingsbereich FROM Trainingseinheit ORDER BY Id;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                ct.ThrowIfCancellationRequested();
                var value = reader.GetString(1);
                if (VergleicheNamen.Ausfuehren(value, name)) rows.Add(new(reader.GetInt64(0), value, "Tour"));
            }
        }
        else if (art == Textlistenart.Taetigkeit)
        {
            using var command = c.CreateCommand();
            command.Transaction = tx;
            command.CommandText = "SELECT Id, Taetigkeit, Typ FROM Wartung ORDER BY Id;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                ct.ThrowIfCancellationRequested();
                var value = reader.GetString(1);
                var type = reader.GetString(2);
                if (type != nameof(WartungsTyp.Wartung) && type != nameof(WartungsTyp.Alarm))
                    throw Invalid("Wartungstyp in der Nutzungsvorschau ist ungültig.");
                if (VergleicheNamen.Ausfuehren(value, name)) rows.Add(new(reader.GetInt64(0), value, type));
            }
        }
        else throw Invalid();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { Art = art, Name = name, Rows = rows });
        var usageVersion = Convert.ToHexString(SHA256.HashData(bytes));
        var tours = art == Textlistenart.Trainingsbereich ? rows.Count : 0;
        var maint = art == Textlistenart.Taetigkeit ? rows.Count(x => x.Art == WartungsTyp.Wartung.ToString()) : 0;
        var alarms = art == Textlistenart.Taetigkeit ? rows.Count(x => x.Art == WartungsTyp.Alarm.ToString()) : 0;
        return new(tours, maint, alarms, usageVersion, rows.Select(row => row.Id).ToArray());
    }

    private static bool IstVerwendet(SqliteConnection c, SqliteTransaction tx, Auswahlliste list, string name, CancellationToken ct)
    {
        var art = list switch
        {
            Auswahlliste.Trainingsbereich => Textlistenart.Trainingsbereich,
            Auswahlliste.Taetigkeit => Textlistenart.Taetigkeit,
            _ => throw Invalid()
        };
        var usage = ErmittleVerwendung(c, tx, art, name, ct);
        return usage.Touren + usage.Wartungen + usage.Alarme > 0;
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
