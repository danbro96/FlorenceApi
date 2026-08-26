using FlorenceApi.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FlorenceApi.Endpoints;

/// <summary>
/// Readiness check: pings the inference worker. Reuses <see cref="FlorenceClient.IsHealthyAsync"/>,
/// which applies its own 3s budget and swallows transport errors to a bool.
/// </summary>
internal sealed class WorkerHealthCheck(FlorenceClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => await client.IsHealthyAsync(cancellationToken)
            ? HealthCheckResult.Healthy("Worker reachable.")
            : HealthCheckResult.Unhealthy("Worker unreachable.");
}
