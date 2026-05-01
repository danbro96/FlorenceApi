using FlorenceApi.Services;

namespace FlorenceApi.Endpoints;

public static class HealthEndpoint
{
    public static IEndpointConventionBuilder MapHealthEndpoint(this IEndpointRouteBuilder app)
    {
        return app.MapGet("/healthz", async (FlorenceClient client, CancellationToken ct) =>
        {
            var ok = await client.IsHealthyAsync(ct);
            return ok
                ? Results.Ok(new { status = "ok" })
                : Results.Problem("Worker unhealthy.", statusCode: 503);
        }).AllowAnonymous();
    }
}
