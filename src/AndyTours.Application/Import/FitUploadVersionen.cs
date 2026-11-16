using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AndyTours.Contracts.V1;

namespace AndyTours.Application.Import;

/// <summary>Creates a deterministic opaque version for the selected upload set.</summary>
public static class FitUploadVersionen
{
    public static string Bestand(IReadOnlyList<FitUploadAntwort> uploads)
    {
        ArgumentNullException.ThrowIfNull(uploads);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var upload in uploads.OrderBy(x => x.UploadId))
            {
                writer.WriteStartArray();
                writer.WriteStringValue(upload.UploadId.ToString("D"));
                writer.WriteStringValue(upload.Version);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }
        return Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
    }
}
