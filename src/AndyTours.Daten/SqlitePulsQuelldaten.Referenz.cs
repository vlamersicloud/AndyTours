using System.Security.Cryptography;
using AndyTours.Application.Auswertung;
using AndyTours.Application.Stammdaten;
using AndyTours.Core.Modell;

namespace AndyTours.Daten;

public sealed partial class SqlitePulsQuelldaten : IPulsQuelldaten
{
    public Task<PulsReferenzServerbestand> ReferenzSpeichern(
        string erwarteteVersion, GarminPulszonenStand stand, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IstVersion(erwarteteVersion) || !IstGueltigeReferenz(stand, erlaubeLeereProfile: false))
            throw Ungueltig();

        using var db = OeffneDatenbank.Ausfuehren(_datenbankpfad);
        using var tx = db.BeginTransaction(deferred: false);
        var current = ReadReference(db, tx);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(erwarteteVersion), Convert.FromHexString(current.Version)))
            throw Konflikt();

        cancellationToken.ThrowIfCancellationRequested();
        GarminPulszonenEinstellung.Speichern(db, tx, stand);
        var updated = ReadReference(db, tx);
        cancellationToken.ThrowIfCancellationRequested();
        tx.Commit();
        return Task.FromResult(updated);
    }

    private static bool IstVersion(string? version) =>
        version is { Length: 64 } && version.All(Uri.IsHexDigit);

}
