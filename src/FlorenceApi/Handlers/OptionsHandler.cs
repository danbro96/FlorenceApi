using FlorenceApi.Models;
using FlorenceApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FlorenceApi.Handlers;

public sealed class OptionsHandler
{
    private const string CacheKey = "worker-options";
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);
    private static readonly string[] _supportedFormats = ["jpeg", "png", "webp"];

    private readonly FlorenceClient _client;
    private readonly IMemoryCache _cache;
    private readonly int _maxImageBytes;

    public OptionsHandler(
        FlorenceClient client,
        IMemoryCache cache,
        IOptions<FlorenceOptions> florenceOptions)
    {
        _client = client;
        _cache = cache;
        _maxImageBytes = florenceOptions.Value.MaxImageBytes;
    }

    public async Task<Results<Ok<OptionsResponse>, ProblemHttpResult>>
        GetAsync(CancellationToken ct)
    {
        string? raw;
        try
        {
            raw = await _cache.GetOrCreateAsync(CacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheTtl;
                return await _client.GetOptionsAsync(ct);
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
            MaxImageBytes = _maxImageBytes,
            SupportedFormats = _supportedFormats,
        });
    }
}
