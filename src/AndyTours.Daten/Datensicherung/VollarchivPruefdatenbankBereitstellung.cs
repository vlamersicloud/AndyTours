namespace AndyTours.Daten.Datensicherung;

using System.Collections.ObjectModel;
using System.Security;
using System.Security.Cryptography;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;
using AndyTours.Daten;


/// <summary>
/// Creates an isolated inspection copy for verified full archives only. Requires trusted server roots,
/// caller-held outer lease, and independent exclusion of external writers; it does not validate SQLite or restore.
/// </summary>
public sealed class VollarchivPruefdatenbankBereitstellung
{
    private readonly DateisystemSicherungskatalog _katalog;
    private readonly string _pruefordner;
    private readonly string _aktiverDatenordner;
    private readonly SicherungsarchivGrenzen _grenzen;
    private readonly ReadOnlyCollection<string> _ausgeschlossenePfade;

    public VollarchivPruefdatenbankBereitstellung(
        DateisystemSicherungskatalog katalog,
        string festerPruefordner,
        string aktiverDatenordner,
        SicherungsarchivGrenzen grenzen,
        IReadOnlyList<string> ausgeschlosseneRelativePfade)
    {
        ArgumentNullException.ThrowIfNull(katalog);
        ArgumentNullException.ThrowIfNull(grenzen);
        ArgumentNullException.ThrowIfNull(ausgeschlosseneRelativePfade);
        _pruefordner = CanonicalRoot(festerPruefordner);
        _aktiverDatenordner = CanonicalRoot(aktiverDatenordner);
        ValidateLimits(grenzen);
        _katalog = katalog;
        _grenzen = grenzen;
        _ausgeschlossenePfade = Array.AsReadOnly(ausgeschlosseneRelativePfade.ToArray());
    }

    public async Task<BereitgestelltePruefdatenbank> Bereitstellen(
        WiederherstellungsvorschauAnfrage anfrage,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = WiederherstellungsvorschauRegeln.PruefeAnfrage(anfrage);
        WiederherstellungsPruefarbeitsbereich? workspace = null;
        SicherungsquellenLease? source = null;
        try
        {
            var listing = await _katalog.Liste(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var matches = listing.Eintraege.Where(entry => entry.Id == request.SicherungsId).ToArray();
            if (matches.Length == 0) throw Missing();
            if (matches.Length != 1) throw Changed();
            var metadata = matches[0];
            if (!WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig(metadata))
                throw Unavailable();
            if (metadata.Art != Sicherungsart.Vollarchiv)
                throw InvalidKind();
            if (!string.Equals(metadata.InhaltHash, request.ErwarteterInhaltHash, StringComparison.OrdinalIgnoreCase))
                throw Changed();

            source = await _katalog.OeffnePruefquelle(request, _grenzen.MaxArchivBytes, cancellationToken).ConfigureAwait(false);
            if (source.Metadaten != metadata || source.Metadaten.Art != Sicherungsart.Vollarchiv)
                throw Changed();

            try
            {
                workspace = WiederherstellungsPruefarbeitsbereich.Oeffnen(
                    _pruefordner, _aktiverDatenordner, Guid.NewGuid(), cancellationToken);
            }
            catch (StammdatenException) { throw Unavailable(); }

            Sicherungsmanifest manifest;
            try
            {
                manifest = await SicherungsdatenbankExtraktion.Kopiere(
                    source.Inhalt, workspace.DatenbankZiel, _grenzen, _ausgeschlossenePfade,
                    request.ErwarteterInhaltHash, cancellationToken).ConfigureAwait(false);
            }
            catch (StammdatenException) { throw Unavailable(); }
            if (manifest.ErstelltUtc != metadata.ErstelltUtc) throw Changed();
            await workspace.SchliesseAusgabe(cancellationToken).ConfigureAwait(false);

            var sourceMetadata = source.Metadaten;
            await source.DisposeAsync().ConfigureAwait(false);
            source = null;
            cancellationToken.ThrowIfCancellationRequested();

            var result = new BereitgestelltePruefdatenbank(workspace, sourceMetadata, manifest);
            workspace = null;
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (StammdatenException) { throw; }
        catch (SicherungskatalogNichtVerfuegbarException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or
                                        ArgumentException or NotSupportedException or OverflowException or CryptographicException)
        {
            throw Unavailable();
        }
        finally
        {
            try
            {
                if (source is not null)
                {
                    try { await source.DisposeAsync().ConfigureAwait(false); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (SecurityException) { }
                    catch (ObjectDisposedException) { }
                }
            }
            finally { workspace?.Dispose(); }
        }
    }

    private static string CanonicalRoot(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw Unavailable();
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or SecurityException)
        {
            throw Unavailable();
        }
    }

    private static void ValidateLimits(SicherungsarchivGrenzen limits)
    {
        var manifest = limits.Manifest;
        if (manifest is null || limits.MaxArchivBytes <= 0 || limits.MaxManifestBytes <= 0 ||
            limits.MaxManifestBytes > Array.MaxLength || limits.MaxManifestBytes > limits.MaxArchivBytes ||
            limits.MaxZentralverzeichnisBytes <= 0 || limits.MaxZentralverzeichnisBytes > limits.MaxArchivBytes ||
            manifest.MaxEintraege <= 0 || manifest.MaxEintraege >= int.MaxValue ||
            manifest.MaxDateiBytes <= 0 || manifest.MaxGesamtBytes <= 0 ||
            manifest.MaxDateiBytes > manifest.MaxGesamtBytes || manifest.MaxPfadzeichen <= 0)
            throw Unavailable();
    }

    private static StammdatenException Missing() =>
        new(StammdatenFehlerart.NichtGefunden, "Sicherungsquelle nicht gefunden.");

    private static StammdatenException Changed() =>
        new(StammdatenFehlerart.Konflikt, "Sicherungsquelle wurde geändert.");

    private static StammdatenException InvalidKind() =>
        new(StammdatenFehlerart.Ungueltig, "Sicherungsart wird für diese Prüfung nicht unterstützt.");

    private static SicherungskatalogNichtVerfuegbarException Unavailable() => new();
}
