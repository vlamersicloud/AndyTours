namespace AndyTours.Api;

using AndyTours.Application.Koordination;
using Microsoft.AspNetCore.Http;

public sealed record ExklusiverDatenbestand;

/// <summary>
/// Acquires the in-process dataset lease for an already authenticated request.
/// This middleware does not coordinate with other processes or external SQLite writers.
/// </summary>
public sealed class DatenbestandsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IDatenbestandskoordination koordination)
    {
        if (context.Request.Path.Equals("/api/v1/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        IDisposable lease;
        try
        {
            lease = context.GetEndpoint()?.Metadata.GetMetadata<ExklusiverDatenbestand>() is not null
                ? await koordination.ExklusivBetreten(context.RequestAborted).ConfigureAwait(false)
                : await koordination.Betreten(context.RequestAborted).ConfigureAwait(false);
        }
        catch (DatenbestandBelegtException)
        {
            await Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Datenbestand ist vorübergehend belegt.")
                .ExecuteAsync(context)
                .ConfigureAwait(false);
            return;
        }

        using (lease)
            await next(context).ConfigureAwait(false);
    }
}
