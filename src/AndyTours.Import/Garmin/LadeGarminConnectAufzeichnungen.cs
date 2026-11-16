using System.Globalization;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace AndyTours.Import.Garmin;

/// <summary>Lädt noch nicht zugeordnete Aktivitäten und bewahrt Original-ZIPs dauerhaft im Datenordner auf.</summary>
public static class LadeGarminConnectAufzeichnungen
{
    /// <summary>
    /// Verarbeitet höchstens 20 neue Kandidaten aus derselben fortlaufenden Liste.
    /// Der Aufrufer behält den Enumerator zwischen den Paketen und kann fertige Touren sofort speichern.
    /// </summary>
    /// <remarks>
    /// Bei Netzfehler, Ratenlimit oder Zeitlimit werden fertige Dateien zurückgegeben statt verworfen.
    /// Nur bestätigte Trainingseinheiten gelten dauerhaft als importiert; Archive bleiben wiederverwendbar.
    /// </remarks>
    public static async Task<GarminDownloadPaket> LadePaketAsync(
        GarminPersonalClient client, IAsyncEnumerator<long> ids, string archivordner, string zwischenordner, ISet<long> vorhanden,
        IProgress<(int Aktuell, int Gesamt)>? fortschritt = null, IProgress<string>? warnungen = null, CancellationToken cancellationToken = default)
    {
        const int paketgroesse = 20;
        var ergebnis = new List<(long, string)>();
        try
        {
            Directory.CreateDirectory(archivordner);
            Directory.CreateDirectory(zwischenordner);
            for (int versuch = 0; versuch < paketgroesse;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await ids.MoveNextAsync().ConfigureAwait(false))
                {
                    return new GarminDownloadPaket(ergebnis, false, false);
                }

                long id = ids.Current;
                if (vorhanden.Contains(id))
                {
                    continue;
                }

                versuch++;
                fortschritt?.Report((versuch - 1, paketgroesse));
                string name = "activity-" + id.ToString(CultureInfo.InvariantCulture);
                string archiv = Path.Combine(archivordner, name + ".zip");
                if (!File.Exists(archiv))
                {
                    // Kein paralleler Anfragesturm; auch innerhalb eines Pakets die Garmin-API schonen.
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
                    string teil = archiv + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        await using (var ziel = new FileStream(teil, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            await client.LadeArchivAsync(id, ziel, cancellationToken).ConfigureAwait(false);
                        }

                        File.Move(teil, archiv);
                    }
                    finally
                    {
                        if (File.Exists(teil))
                        {
                            File.Delete(teil);
                        }
                    }
                }

                string pfad = Path.Combine(zwischenordner, name + ".fit");
                try
                {
                    await EntpackeFitAsync(archiv, pfad, cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidDataException fehler)
                {
                    File.Delete(archiv);
                    warnungen?.Report($"{name}: {fehler.Message}");
                    fortschritt?.Report((versuch, paketgroesse));
                    continue;
                }

                ergebnis.Add((id, pfad));
                fortschritt?.Report((versuch, paketgroesse));
            }

            return new GarminDownloadPaket(ergebnis, true, false);
        }
        catch (Exception fehler) when (fehler is HttpRequestException or OperationCanceledException
            or IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            string grund = fehler is OperationCanceledException
                ? "Zeitlimit beim Download erreicht."
                : fehler.Message;
            warnungen?.Report($"Garmin-Download unterbrochen: {grund} "
                + "Fertige Aufzeichnungen dieses Pakets können importiert werden. "
                + "Zum Fortsetzen den Garmin-Import später erneut aufrufen; fertige ZIPs bleiben erhalten.");
            return new GarminDownloadPaket(ergebnis, false, true);
        }
    }

    /// <summary>Entnimmt genau eine FIT-Datei ohne ZIP-Pfade zu verwenden (Schutz vor Pfadtraversierung und ZIP-Bomben).</summary>
    public static async Task EntpackeFitAsync(string archiv, string ziel, CancellationToken cancellationToken = default)
    {
        using ZipArchive zip = ZipFile.OpenRead(archiv);
        ZipArchiveEntry[] dateien = zip.Entries.Where(e => e.Name.EndsWith(".fit", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (dateien.Length != 1)
        {
            throw new InvalidDataException("Das Garmin-ZIP muss genau eine FIT-Aufzeichnung enthalten.");
        }

        if (dateien[0].Length > 128 * 1024 * 1024)
        {
            throw new InvalidDataException("Die entpackte FIT-Datei ist zu groß.");
        }

        await using Stream quelle = dateien[0].Open();
        await using var ausgabe = new FileStream(ziel, FileMode.Create, FileAccess.Write, FileShare.None);
        await GarminPersonalClient.KopiereBegrenztAsync(quelle, ausgabe, 128 * 1024 * 1024, cancellationToken).ConfigureAwait(false);
    }
}
