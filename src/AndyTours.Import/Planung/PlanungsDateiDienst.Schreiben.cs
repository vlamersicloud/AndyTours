using AndyTours.Core.Modell;
using System.Text;
using System.Xml;
using AndyTours.Application.Planung;
using AndyTours.Application.Stammdaten;
using AndyTours.Core.Berechnungen;
using AndyTours.Import.Planung;
using AndyTours.Contracts.V1;

namespace AndyTours.Import.Planung;

public sealed partial class PlanungsDateiDienst
{
    public Task<PlanungsGpxExportAntwort> Schreiben(PlanungsGpxExportAnfrage anfrage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (anfrage is null || anfrage.Planung is null) throw Invalid();
            string name = PlanungsDateiRegeln.NormalisiereName(anfrage.Name);
            Tourenplanung core = PlanungsVertragMapper.ToCore(anfrage.Planung, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            string xml = PlanungsGpx.Schreiben(core, name);
            cancellationToken.ThrowIfCancellationRequested();
            int byteCount = Encoding.UTF8.GetByteCount(xml);
            if (byteCount > PlanungsDateiRegeln.MaxDateiBytes) throw new PlanungsDateiZuGrossException();
            byte[] bytes = new byte[byteCount];
            Encoding.UTF8.GetBytes(xml, bytes);
            string safeName = BereinigeDateiname.Ausfuehren(name).TrimEnd(' ', '.') + ".gpx";
            if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(safeName))) throw Invalid();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PlanungsGpxExportAntwort(safeName, bytes));
        }
        catch (PlanungsDateiZuGrossException) { throw; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (StammdatenException) { throw; }
        catch (Exception ex) when (ex is XmlException or ArgumentException or FormatException or OverflowException)
        { throw Invalid(); }
    }

}
