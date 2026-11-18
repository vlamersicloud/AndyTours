namespace AndyTours.Client;

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AndyTours.Application.Sicherung;
using AndyTours.Application.Stammdaten;
using AndyTours.Contracts.V1;

/// <summary>Streaming download client that verifies catalog length and SHA-256 without owning the HttpClient or destination.</summary>
public sealed class SicherungsdownloadHttpClient(HttpClient http) : ISicherungsdownloadClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    public async Task Download(SicherungAntwort selected, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        if (!WiederherstellungsvorschauRegeln.IstKatalogeintragGueltig(selected) || !destination.CanWrite)
            throw new InvalidDataException("Sicherungsdownloadziel oder Katalogeintrag ist ungültig.");

        var requestContract = new SicherungsdownloadAnfrage(selected.Id, selected.InhaltHash);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/sicherungen/download")
        {
            Content = JsonContent.Create(requestContract, options: JsonOptions)
        };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or
            HttpStatusCode.Conflict or HttpStatusCode.ServiceUnavailable)
            throw new HttpRequestException("Sicherungsdownload wurde abgelehnt.", null, response.StatusCode);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Sicherungsdownload fehlgeschlagen.", null, response.StatusCode);
        if (response.Content.Headers.ContentLength != selected.Byteanzahl)
            throw new InvalidDataException("Sicherungsdownloadgröße stimmt nicht mit dem Katalog überein.");

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            total = checked(total + read);
            if (total > selected.Byteanzahl)
                throw new InvalidDataException("Sicherungsdownload überschreitet die Kataloggröße.");
            hash.AppendData(buffer, 0, read);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (total != selected.Byteanzahl)
            throw new InvalidDataException("Sicherungsdownload ist unvollständig.");

        var actualHash = Convert.ToHexString(hash.GetHashAndReset());
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(selected.InhaltHash), Convert.FromHexString(actualHash)))
            throw new InvalidDataException("Sicherungsdownloadhash stimmt nicht mit dem Katalog überein.");
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
