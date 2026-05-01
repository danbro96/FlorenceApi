using System.Text.Json.Nodes;
using FlorenceApi.Models;
using FlorenceApi.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FlorenceApi.Endpoints;

public static class OptionsEndpoint
{
    const string CacheKey = "worker-options";
    static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    static readonly string[] SupportedFormats = ["jpeg", "png", "webp"];

    public static IEndpointConventionBuilder MapOptionsEndpoint(this IEndpointRouteBuilder app)
    {
        return app.MapGet("/options", async (
            FlorenceClient client,
            IMemoryCache cache,
            IOptions<FlorenceOptions> opts,
            CancellationToken ct) =>
        {
            string? raw;
            try
            {
                raw = await cache.GetOrCreateAsync(CacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                    return await client.GetOptionsAsync(ct);
                });
            }
            catch (HttpRequestException)
            {
                return Results.Problem("Worker unavailable.", statusCode: 503);
            }

            var node = raw is null ? null : JsonNode.Parse(raw);
            if (node is null)
                return Results.Problem("Worker returned invalid options.", statusCode: 502);

            node["max_image_bytes"] = opts.Value.MaxImageBytes;
            var formats = new JsonArray();
            foreach (var f in SupportedFormats) formats.Add(f);
            node["supported_formats"] = formats;

            return Results.Content(node.ToJsonString(), "application/json");
        });
    }
}
