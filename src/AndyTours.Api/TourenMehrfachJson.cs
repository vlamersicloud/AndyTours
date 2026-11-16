using System.Text.Json;
using AndyTours.Contracts.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AndyTours.Api;

internal static class TourenMehrfachJson
{
    private const long MaxRequestBytes = 128L * 1024 * 1024;

    internal static async Task<IResult> Ausfuehren<T>(
        HttpContext context,
        IOptions<JsonOptions> options,
        Func<T, CancellationToken, Task<TourenMehrfachAntwort>> operation,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
            sizeFeature.MaxRequestBodySize = MaxRequestBytes;

        if (context.Request.ContentLength > MaxRequestBytes)
            return ZuGross();

        if (!context.Request.HasJsonContentType())
            return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "JSON-Inhalt erforderlich.");

        T? request;
        try
        {
            using var body = new BegrenzterRequestLesestream(context.Request.Body, MaxRequestBytes);
            request = await JsonSerializer.DeserializeAsync<T>(
                body, options.Value.SerializerOptions, cancellationToken);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return ZuGross();
        }
        catch (JsonException)
        {
            return Ungueltig();
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (request is null)
            return Ungueltig();

        return Results.Ok(await operation(request, cancellationToken));
    }

    private static IResult ZuGross() =>
        Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
            title: "Anfrage zu groß.",
            extensions: new Dictionary<string, object?> { ["code"] = "touren.mehrfach_zu_gross" });

    private static IResult Ungueltig() =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            title: "Mehrfachaktion-Anfrage ungültig.");
}
