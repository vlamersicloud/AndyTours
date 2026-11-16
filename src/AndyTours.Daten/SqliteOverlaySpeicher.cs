using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndyTours.Application.Overlays;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Read-only overlay snapshot reader over one normalized absolute SQLite path.</summary>
public sealed partial class SqliteOverlaySpeicher
{
    private readonly string _datenbankpfad;

    public SqliteOverlaySpeicher(string datenbankpfad)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datenbankpfad);
        if (!Path.IsPathFullyQualified(datenbankpfad))
            throw new ArgumentException("Ein absoluter Datenbankpfad ist erforderlich.", nameof(datenbankpfad));
        _datenbankpfad = Path.GetFullPath(datenbankpfad);
    }

    /// <inheritdoc />
    public Task<OverlayBestandAntwort> Laden(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: true);
        var bestand = ReadBestand(db, tx, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(bestand);
    }

    /// <summary>Reads metadata and hashes point blobs without decoding or materializing geometry.</summary>
    internal static OverlayBestandAntwort ReadBestand(SqliteConnection db, SqliteTransaction tx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var folders = new List<OverlayOrdnerAntwort>();
        using (var command = db.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = "SELECT Id, ElternId, Name FROM Overlayordner ORDER BY Id;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                ct.ThrowIfCancellationRequested();
                folders.Add(new OverlayOrdnerAntwort(reader.GetInt64(0),
                    reader.IsDBNull(1) ? null : reader.GetInt64(1), reader.GetString(2)));
            }
        }

        var items = new List<OverlayMetadatenAntwort>();
        using (var command = db.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = "SELECT Id, OrdnerId, Name, Farbe, Eingeschaltet, Punkte FROM Overlay ORDER BY Id;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                ct.ThrowIfCancellationRequested();
                long id = reader.GetInt64(0);
                long? folderId = reader.IsDBNull(1) ? null : reader.GetInt64(1);
                string name = reader.GetString(2);
                string? rawColor = reader.IsDBNull(3) ? null : reader.GetString(3);
                bool enabled = reader.GetBoolean(4);
                string geometryHash = HashBlob(reader.GetStream(5), ct);
                string itemVersion = HashJson(new { id, folderId, name, rawColor, enabled, geometryHash });
                items.Add(new OverlayMetadatenAntwort(id, folderId, name, Farbe(rawColor),
                    enabled, itemVersion));
            }
        }

        string? commonRaw;
        using (var command = db.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = "SELECT Wert FROM Einstellung WHERE Schluessel = $key LIMIT 1;";
            command.Parameters.AddWithValue("$key", Einstellungsschluessel.OverlayFarbe);
            commonRaw = command.ExecuteScalar() as string;
        }
        var commonColor = Farbe(commonRaw) ?? OverlayFarbe.Violett;
        string aggregate = HashJson(new
        {
            folders = folders.Select(f => new { f.Id, f.ElternId, f.Name }),
            items = items.Select(i => new { i.Id, i.OrdnerId, i.Name, color = (int?)i.Farbe, i.IstEingeschaltet, i.Version }),
            commonColor = (int)commonColor,
            commonRaw
        });
        return new OverlayBestandAntwort(aggregate, commonColor, folders, items);
    }

    internal static string HashBlob(Stream stream, CancellationToken ct)
    {
        using (stream)
        using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            byte[] buffer = new byte[64 * 1024];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                hash.AppendData(buffer, 0, read);
            }
            return Convert.ToHexString(hash.GetHashAndReset());
        }
    }

    internal static string HashJson<T>(T value) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    private static OverlayFarbe? Farbe(string? raw)
    {
        if (!Enum.TryParse(raw, out AndyTours.Core.Modell.Overlayfarbe coreColor) || !Enum.IsDefined(coreColor))
            return null;
        return (OverlayFarbe)(int)coreColor;
    }
}
