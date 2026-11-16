using AndyTours.Application.Touren;
using AndyTours.Contracts.V1;
using AndyTours.Core.Berechnungen;
using AndyTours.Core.Modell;
using Microsoft.Data.Sqlite;

namespace AndyTours.Daten;

/// <summary>Persistiert atomare Mehrfachänderungen auf Touren ohne Vollbestands-Overwrite.</summary>
public sealed partial class SqliteTourenMehrfachverwaltung
{
    /// <summary>Setzt und entfernt Kategorien aller referenzierten Touren atomar.</summary>
    public Task<TourenMehrfachAntwort> Kategorien(TourenKategorienAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = TourenMehrfachRegeln.Normalisieren(anfrage);
        var hash = Hash("kategorien", normalized);
        var response = Run(normalized.AnfrageId, hash, normalized.Touren, (connection, transaction, selected) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var categories = KategorienTabelle.Laden(connection).Select(x => x.Id).ToHashSet();
            if (normalized.Setzen.Concat(normalized.Entfernen).Any(id => !categories.Contains(id)))
                throw UngueltigesZiel();

            var changed = OrdneEinheitenZu.Kategorien(selected, normalized.Setzen, normalized.Entfernen);
            SchreibeAenderungen(connection, transaction, selected, changed, cancellationToken);
            return AntwortGeaenderter(connection, selected, normalized.AnfrageId, cancellationToken);
        }, cancellationToken);
        return Task.FromResult(response);
    }

    /// <summary>Ändert die Aktivität; vorhandenes Equipment bleibt unverändert.</summary>
    public Task<TourenMehrfachAntwort> Aktivitaet(TourenAktivitaetAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = TourenMehrfachRegeln.Normalisieren(anfrage);
        var hash = Hash("aktivitaet", normalized);
        var response = Run(normalized.AnfrageId, hash, normalized.Touren, (connection, transaction, selected) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!AktivitaetenTabelle.Laden(connection).Any(x => x.Id == normalized.AktivitaetId))
                throw UngueltigesZiel();

            var changed = OrdneEinheitenZu.Aktivitaet(selected, normalized.AktivitaetId);
            SchreibeAenderungen(connection, transaction, selected, changed, cancellationToken);
            return AntwortGeaenderter(connection, selected, normalized.AnfrageId, cancellationToken);
        }, cancellationToken);
        return Task.FromResult(response);
    }

    /// <summary>Setzt oder entfernt Equipment; Kategorien bleiben unverändert.</summary>
    public Task<TourenMehrfachAntwort> Equipment(TourenEquipmentAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = TourenMehrfachRegeln.Normalisieren(anfrage);
        var hash = Hash("equipment", normalized);
        var response = Run(normalized.AnfrageId, hash, normalized.Touren, (connection, transaction, selected) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (normalized.EquipmentId is { } equipmentId)
            {
                var bestand = DatenbestandTabellen.Laden(connection);
                var activityIds = selected.Select(x => x.AktivitaetId).Distinct().ToArray();
                var available = ErmittleEquipmentauswahl.Ausfuehren(bestand, activityIds);
                if (!available.Any(x => x.Id == equipmentId))
                    throw UngueltigesZiel();
            }

            var changed = OrdneEinheitenZu.Equipment(selected, normalized.EquipmentId);
            SchreibeAenderungen(connection, transaction, selected, changed, cancellationToken);
            return AntwortGeaenderter(connection, selected, normalized.AnfrageId, cancellationToken);
        }, cancellationToken);
        return Task.FromResult(response);
    }

    private static void SchreibeAenderungen(
        SqliteConnection connection, SqliteTransaction transaction,
        IReadOnlyList<Trainingseinheit> selected, List<Trainingseinheit> changed,
        CancellationToken cancellationToken)
    {
        if (selected.Count != changed.Count)
            throw new InvalidOperationException("Mehrfachaktion lieferte eine ungültige Antwort.");
        for (var index = 0; index < changed.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (selected[index].Id != changed[index].Id)
                throw new InvalidOperationException("Mehrfachaktion lieferte eine ungültige Antwort.");
            TrainingseinheitenTabelle.AendereInTransaktion(connection, transaction, changed[index]);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static TourenMehrfachAntwort AntwortGeaenderter(
        SqliteConnection connection, IReadOnlyList<Trainingseinheit> selected,
        Guid anfrageId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ids = selected.Select(x => x.Id).ToHashSet();
        var current = TrainingseinheitenTabelle.Laden(connection)
            .Where(x => ids.Contains(x.Id))
            .OrderBy(x => x.Id)
            .Select(TourenMapper.ToAntwort)
            .ToArray();
        if (current.Length != ids.Count)
            throw new InvalidOperationException("Mehrfachaktion lieferte eine ungültige Antwort.");
        cancellationToken.ThrowIfCancellationRequested();
        return new TourenMehrfachAntwort(anfrageId, Array.AsReadOnly(current), Array.AsReadOnly(Array.Empty<long>()));
    }

    private static UngueltigeTourException UngueltigesZiel() =>
        new("Mehrfachaktion ist ungültig.");
}
