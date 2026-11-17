using AndyTours.Contracts.V1;

namespace AndyTours.Daten.Datensicherung;

/// <summary>Performs only the isolated, read-only SQLite base inspection for a prepared full archive.</summary>
public sealed class SqlitePruefdienst
{
    private readonly VollarchivPruefdatenbankBereitstellung _bereitstellung;
    private readonly SqlitePruefRunner _runner;

    public SqlitePruefdienst(VollarchivPruefdatenbankBereitstellung bereitstellung, SqlitePruefRunner runner)
    {
        ArgumentNullException.ThrowIfNull(bereitstellung);
        ArgumentNullException.ThrowIfNull(runner);
        _bereitstellung = bereitstellung;
        _runner = runner;
    }

    public async Task<SqlitePruefprotokoll> PruefenAsync(
        WiederherstellungsvorschauAnfrage anfrage, CancellationToken cancellationToken = default)
    {
        var prepared = await _bereitstellung.Bereitstellen(anfrage, cancellationToken).ConfigureAwait(false);
        try
        {
            return await _runner.PruefenAsync(prepared, cancellationToken).ConfigureAwait(false);
        }
        catch (SqlitePruefQuarantaeneException)
        {
            // Runner registry took strong ownership of the child and this staging workspace.
            throw;
        }
        finally
        {
            if (!SqlitePruefQuarantaeneIstUebernommen(prepared))
                prepared.Dispose();
        }
    }

    private static bool SqlitePruefQuarantaeneIstUebernommen(BereitgestelltePruefdatenbank stage) =>
        SqlitePruefQuarantaene.Haelt(stage);
}
