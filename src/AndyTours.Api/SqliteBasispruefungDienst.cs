namespace AndyTours.Api;

using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten;
using AndyTours.Daten.Datensicherung;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

/// <summary>Explicit non-destructive adapter to the isolated database checker.</summary>
public sealed class SqliteBasispruefungDienst : ISqliteBasispruefung
{
    private readonly IConfiguration _configuration;
    private readonly string _dataRoot;
    private readonly ISicherungskatalog _catalog;
    public SqliteBasispruefungDienst(IConfiguration configuration, string dataRoot, ISicherungskatalog catalog)
    { _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration)); _dataRoot = dataRoot; _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); }

    public async Task<SqliteBasispruefungAntwort> Pruefen(SqliteBasispruefungAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (anfrage is null || anfrage.SicherungsId == Guid.Empty || !IsSha256(anfrage.ErwarteterInhaltHash))
            throw new StammdatenException(StammdatenFehlerart.Ungueltig, "Prüfanfrage ist ungültig.");
        if (!SqliteBasispruefungKonfiguration.TryLesen(_configuration, _dataRoot, out var config) || config is null)
            throw new SicherungskatalogNichtVerfuegbarException();
        try
        {
            if (_catalog is not DateisystemSicherungskatalog fileCatalog) throw new SicherungskatalogNichtVerfuegbarException();
            var stager = new VollarchivPruefdatenbankBereitstellung(fileCatalog, config.Pruefordner, _dataRoot, config.Archivgrenzen, Array.Empty<string>());
            var runner = new SqlitePruefRunner(config.CheckerExecutable, TimeSpan.FromMilliseconds(config.LaufzeitMillisekunden),
                config.MaxOutputBytes, config.MaxDatenbankBytes, new SystemPruefprozessStarter());
            var result = await new SqlitePruefdienst(stager, runner).PruefenAsync(
                new WiederherstellungsvorschauAnfrage(anfrage.SicherungsId, anfrage.ErwarteterInhaltHash), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (result.Status is not ("ok" or "invalid_input" or "readonly_unavailable" or "unsupported_schema" or "core_schema_invalid" or "integrity_failed" or "check_failed"))
                throw new SicherungskatalogNichtVerfuegbarException();
            return new SqliteBasispruefungAntwort(anfrage.SicherungsId, result.Status, result.SchemaVersion,
                result.CoreTablesChecked, result.QuickCheck == "ok", Array.AsReadOnly(Array.Empty<string>()));
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw; }
        catch (SicherungskatalogNichtVerfuegbarException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Security.SecurityException or CryptographicException)
        { throw new SicherungskatalogNichtVerfuegbarException(); }
    }
    private static bool IsSha256(string? value)
    {
        if (value is null || value.Length != 64) return false;
        Span<byte> digest = stackalloc byte[32];
        return value.All(Uri.IsHexDigit);
    }
}
