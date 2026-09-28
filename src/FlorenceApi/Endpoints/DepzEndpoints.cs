using System.Text.Json;
using System.Text.Json.Serialization;
using FlorenceApi.Dependencies;

namespace FlorenceApi.Endpoints;

/// <summary>Non-gating dependency report: this service's outward auth seams, served from the
/// poller's cache. Deliberately not part of /readyz.</summary>
public static class DepzEndpoints
{
    // devops-api parses the camelCase/PascalCase-enum shape every other service emits, not this API's snake_case.
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static void MapDepz(this IEndpointRouteBuilder app) =>
        app.MapGet("/depz", (DependencyReportCache cache) => TypedResults.Json(cache.Current(), WireJson))
            .AllowAnonymous()
            .AddEndpointFilter<ProbeKeyFilter>()
            .ExcludeFromDescription()
            .DisableHttpMetrics()
            .WithName("GetDependencies");
}
