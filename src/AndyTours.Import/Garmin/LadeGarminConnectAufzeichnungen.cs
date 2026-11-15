using System.Globalization;
using System.IO.Compression;

namespace AndyTours.Import.Garmin;

/// <summary>Lädt noch nicht zugeordnete Aktivitäten und bewahrt Original-ZIPs dauerhaft im Datenordner auf.</summary>
public static class LadeGarminConnectAufzeichnungen
{
    /// <summary>Erzeugt je Aktivitäts-ID eine FIT-Datei und liefert deren Pfad für den vorhandenen FIT-Import.</summary>
    /// <remarks>
    /// Bereits heruntergeladene ZIPs werden nach Abbruch wiederverwendet. Eine ID gilt hier noch nicht als importiert;
    /// erst die bestätigte Speicherung der Trainingseinheit legt die dauerhafte Zuordnung an.
    /// </remarks>
    public static async Task<IReadOnlyList<(long Id, string Pfad)>> AusfuehrenAsync(
        GarminPersonalClient client, string archivordner, string zwischenordner, ISet<long> vorhanden,
        IProgress<(int Aktuell, int Gesamt)>? fortschritt = null, IProgress<string>? warnungen = null, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<long> ids = await client.LeseRadaktivitaetenAsync(cancellationToken).ConfigureAwait(false);
        long[] neue = ids.Where(id => !vorhanden.Contains(id)).ToArray();
        Directory.CreateDirectory(archivordner);
        Directory.CreateDirectory(zwischenordner);
        var ergebnis = new List<(long, string)>();
        for (int i = 0; i < neue.Length; i++)
        {
            long id = neue[i];
            string name = "activity-" + id.ToString(CultureInfo.InvariantCulture);
            string archiv = Path.Combine(archivordner, name + ".zip");
            if (!File.Exists(archiv))
            {
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
                // Defekte bzw. nicht passende Antworten dürfen nicht dauerhaft im Downloadcache feststecken.
                File.Delete(archiv);
                warnungen?.Report($"{name}: {fehler.Message}");
                continue;
            }

            ergebnis.Add((id, pfad));
            fortschritt?.Report((i + 1, neue.Length));
        }

        return ergebnis;
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
