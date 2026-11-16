using System.Xml;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Core.Berechnungen;
using AndyTours.Import.Planung;

namespace AndyTours.Import.Planung;

public sealed partial class PlanungsDateiDienst
{
    public static async Task<AndyTours.Contracts.V1.PlanungsStand> Lesen(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is null || !stream.CanRead) throw Invalid();
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[64 * 1024];
            long total = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                total += read;
                if (total > PlanungsDateiRegeln.MaxDateiBytes) throw new PlanungsDateiZuGrossException();
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (total == 0) throw Invalid();
            buffer.Position = 0;
            var settings = new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = PlanungsDateiRegeln.MaxDateiBytes,
                CloseInput = false
            };
            using XmlReader reader = XmlReader.Create(buffer, settings);
            var core = await PlanungsGpx.Lesen(reader, cancellationToken).ConfigureAwait(false);
            if (core is null) throw Invalid();
            cancellationToken.ThrowIfCancellationRequested();
            return PlanungsVertragMapper.ToDto(core, cancellationToken);
        }
        catch (PlanungsDateiZuGrossException) { throw; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (StammdatenException) { throw; }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or XmlException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
        { throw Invalid(); }
    }

    private static StammdatenException Invalid() => new(StammdatenFehlerart.Ungueltig, "Planungsdatei ungültig.");
}
