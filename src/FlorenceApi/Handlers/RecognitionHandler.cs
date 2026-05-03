using FlorenceApi.Models;
using FlorenceApi.Models.Enums;
using FlorenceApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace FlorenceApi.Handlers;

public sealed class RecognitionHandler
{
    private const string DataUrlPrefix = "data:";
    private static readonly ActivitySource _activitySource = new("FlorenceApi.Recognize");

    private readonly FlorenceClient _client;
    private readonly ILogger<RecognitionHandler> _log;
    private readonly int _maxImageBytes;
    private readonly JsonSerializerOptions _json;

    public RecognitionHandler(
        FlorenceClient client,
        ILogger<RecognitionHandler> log,
        IOptions<FlorenceOptions> florenceOptions,
        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions)
    {
        _client = client;
        _log = log;
        _maxImageBytes = florenceOptions.Value.MaxImageBytes;
        _json = jsonOptions.Value.SerializerOptions;
    }

    public Task<Results<Ok<CaptionResponse>, ProblemHttpResult>>
        CaptionAsync(CaptionRequest req, CancellationToken ct) =>
        ExecuteAsync<CaptionResponse>(req, MapCaptionDetail(req.Detail), textInput: null, ct);

    public Task<Results<Ok<DetectionResponse>, ProblemHttpResult>>
        DetectAsync(DetectionRequest req, CancellationToken ct) =>
        ExecuteAsync<DetectionResponse>(req, MapDetectionVariant(req.Variant), textInput: null, ct);

    public Task<Results<Ok<DetectionResponse>, ProblemHttpResult>>
        GroundAsync(GroundingRequest req, CancellationToken ct) =>
        ExecuteAsync<DetectionResponse>(req, "caption_to_phrase_grounding", req.Text, ct);

    public Task<Results<Ok<CaptionResponse>, ProblemHttpResult>>
        OcrAsync(OcrRequest req, CancellationToken ct) =>
        ExecuteAsync<CaptionResponse>(req, "ocr", textInput: null, ct);

    public Task<Results<Ok<OcrRegionsResponse>, ProblemHttpResult>>
        OcrRegionsAsync(OcrRequest req, CancellationToken ct) =>
        ExecuteAsync<OcrRegionsResponse>(req, "ocr_with_region", textInput: null, ct);

    public Task<Results<Ok<SegmentationResponse>, ProblemHttpResult>>
        SegmentAsync(SegmentationRequest req, CancellationToken ct) =>
        ExecuteAsync<SegmentationResponse>(req, "referring_expression_segmentation", req.Text, ct);

    private async Task<Results<Ok<TResponse>, ProblemHttpResult>>
        ExecuteAsync<TResponse>(
            ImageRequest req, string workerTask, string? textInput, CancellationToken ct)
        where TResponse : class
    {
        if (string.IsNullOrWhiteSpace(req.Image))
            return TypedResults.Problem(detail: "image is required", statusCode: 400);

        if (textInput is not null && string.IsNullOrWhiteSpace(textInput))
            return TypedResults.Problem(detail: "text is required", statusCode: 400);

        byte[] decoded;
        try
        {
            decoded = DecodeBase64(req.Image);
        }
        catch (FormatException)
        {
            return TypedResults.Problem(detail: "image is not valid base64", statusCode: 400);
        }

        if (decoded.Length == 0)
            return TypedResults.Problem(detail: "image is empty", statusCode: 400);

        if (decoded.Length > _maxImageBytes)
            return TypedResults.Problem(detail: $"image exceeds {_maxImageBytes} bytes", statusCode: 413);

        using var activity = _activitySource.StartActivity("recognize");
        activity?.SetTag("task", workerTask);
        activity?.SetTag("image.bytes", decoded.Length);
        activity?.SetTag("text_input.length", textInput?.Length ?? 0);

        var sw = Stopwatch.StartNew();
        try
        {
            using var stream = new MemoryStream(decoded, writable: false);
            var contentType = string.IsNullOrWhiteSpace(req.MediaType)
                ? "application/octet-stream"
                : req.MediaType!;

            var json = await _client.RecognizeAsync(stream, contentType, workerTask, textInput, ct);
            var typed = JsonSerializer.Deserialize<TResponse>(json, _json)
                ?? throw new JsonException("worker returned null");

            activity?.SetTag("elapsed_ms", sw.ElapsedMilliseconds);
            return TypedResults.Ok(typed);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return TypedResults.Problem(detail: ex.Message, statusCode: 400);
        }
        catch (HttpRequestException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _log.LogWarning(ex, "Worker call failed for task {Task}", workerTask);
            return TypedResults.Problem(detail: $"worker error: {ex.Message}", statusCode: 502);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "timeout");
            return TypedResults.Problem(detail: "inference timeout", statusCode: 504);
        }
        catch (JsonException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _log.LogError(ex, "Failed to deserialize worker response for task {Task}", workerTask);
            return TypedResults.Problem(detail: "worker returned malformed response", statusCode: 502);
        }
    }

    private static byte[] DecodeBase64(string input)
    {
        // Tolerate the optional data:image/...;base64, prefix some clients add.
        var span = input.AsSpan().Trim();
        if (span.StartsWith(DataUrlPrefix, StringComparison.Ordinal))
        {
            var comma = span.IndexOf(',');
            if (comma >= 0) span = span[(comma + 1)..];
        }

        return Convert.FromBase64String(span.ToString());
    }

    private static string MapCaptionDetail(CaptionDetail detail) => detail switch
    {
        CaptionDetail.Short => "caption",
        CaptionDetail.Detailed => "detailed_caption",
        CaptionDetail.MoreDetailed => "more_detailed_caption",
        _ => throw new ArgumentOutOfRangeException(nameof(detail), detail, null),
    };

    private static string MapDetectionVariant(DetectionVariant variant) => variant switch
    {
        DetectionVariant.Od => "od",
        DetectionVariant.Dense => "dense_region_caption",
        DetectionVariant.Proposal => "region_proposal",
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
    };
}
