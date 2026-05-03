using FlorenceApi.Models;
using FlorenceApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FlorenceApi.Endpoints;

public static class OptionsEndpoint
{
    private const string CacheKey = "worker-options";
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);
    private static readonly string[] _supportedFormats = ["jpeg", "png", "webp"];

    public static IEndpointConventionBuilder MapOptionsEndpoint(this IEndpointRouteBuilder app) =>
        app.MapGet("/options", async Task<Results<Ok<OptionsResponse>, ProblemHttpResult>> (
                FlorenceClient client,
                IMemoryCache cache,
                IOptions<FlorenceOptions> florenceOptions,
                IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions,
                CancellationToken ct) =>
            {
                string? raw;
                try
                {
                    raw = await cache.GetOrCreateAsync(CacheKey, async entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = _cacheTtl;
                        return await client.GetOptionsAsync(ct);
                    });
                }
                catch (HttpRequestException)
                {
                    return TypedResults.Problem(detail: "worker unavailable", statusCode: 503);
                }

                if (string.IsNullOrEmpty(raw))
                    return TypedResults.Problem(detail: "worker returned empty options", statusCode: 502);

                string model, device, revision;
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    var root = doc.RootElement;
                    model = root.TryGetProperty("model", out var m) ? m.GetString() ?? string.Empty : string.Empty;
                    device = root.TryGetProperty("device", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                    revision = root.TryGetProperty("revision", out var r) ? r.GetString() ?? string.Empty : string.Empty;
                }
                catch (JsonException)
                {
                    return TypedResults.Problem(detail: "worker returned malformed options", statusCode: 502);
                }

                return TypedResults.Ok(new OptionsResponse
                {
                    Model = model,
                    Revision = revision,
                    Device = device,
                    MaxImageBytes = florenceOptions.Value.MaxImageBytes,
                    SupportedFormats = _supportedFormats,
                });
            })
            .WithTags("Meta")
            .WithSummary("Runtime options reported by the worker (model, device, limits).")
            .WithDescription(
                """
                Returns the model id and Hugging Face revision currently loaded, the OpenVINO device
                in use (`GPU`, `GPU.0`, `CPU`, …), the max image upload size, and the accepted image
                formats. The list of recognition tasks lives in the OpenAPI spec at `/openapi/v1.json`
                — fetch that for client codegen.
                """);
}
