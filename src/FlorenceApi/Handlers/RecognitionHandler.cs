using FlorenceApi.Models;
using FlorenceApi.Models.Enums;
using FlorenceApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
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

    public async Task<Results<Ok<CaptionResult>, ProblemHttpResult>>
        CaptionAsync(CaptionRequest req, CancellationToken ct)
    {
        var (bytes, imageError) = ValidateAndDecodeImage(req.Image);
        if (imageError is not null) return imageError;

        var workerTask = req.Detail switch
        {
            CaptionDetail.Short => WorkerTask.Caption,
            CaptionDetail.Detailed => WorkerTask.DetailedCaption,
            CaptionDetail.MoreDetailed => WorkerTask.MoreDetailedCaption,
            _ => throw new ArgumentOutOfRangeException(nameof(req.Detail), req.Detail, null),
        };

        using var activity = StartActivity(workerTask, bytes!.Length, textLength: 0);
        try
        {
            var json = await InvokeWorkerAsync(bytes, req.MediaType, workerTask, textInput: null, activity, ct);
            return TypedResults.Ok(new CaptionResult { Caption = ExtractResult<string>(json) });
        }
        catch (Exception ex) when (TryMapWorkerException(ex, workerTask, activity, ct) is { } problem)
        {
            return problem;
        }
    }

    public async Task<Results<Ok<DetectionResult>, ProblemHttpResult>>
        DetectAsync(DetectionRequest req, CancellationToken ct)
    {
        var (bytes, imageError) = ValidateAndDecodeImage(req.Image);
        if (imageError is not null) return imageError;

        var workerTask = req.Variant switch
        {
            DetectionVariant.Od => WorkerTask.Od,
            DetectionVariant.Dense => WorkerTask.DenseRegionCaption,
            DetectionVariant.Proposal => WorkerTask.RegionProposal,
            _ => throw new ArgumentOutOfRangeException(nameof(req.Variant), req.Variant, null),
        };

        using var activity = StartActivity(workerTask, bytes!.Length, textLength: 0);
        try
        {
            var json = await InvokeWorkerAsync(bytes, req.MediaType, workerTask, textInput: null, activity, ct);
            return TypedResults.Ok(ExtractResult<DetectionResult>(json));
        }
        catch (Exception ex) when (TryMapWorkerException(ex, workerTask, activity, ct) is { } problem)
        {
            return problem;
        }
    }

    public async Task<Results<Ok<DetectionResult>, ProblemHttpResult>>
        GroundAsync(GroundingRequest req, CancellationToken ct)
    {
        var (bytes, imageError) = ValidateAndDecodeImage(req.Image);
        if (imageError is not null) return imageError;
        if (string.IsNullOrWhiteSpace(req.Text))
            return TypedResults.Problem(detail: "text is required", statusCode: 400);

        const WorkerTask workerTask = WorkerTask.CaptionToPhraseGrounding;
        using var activity = StartActivity(workerTask, bytes!.Length, req.Text.Length);
        try
        {
            var json = await InvokeWorkerAsync(bytes, req.MediaType, workerTask, req.Text, activity, ct);
            return TypedResults.Ok(ExtractResult<DetectionResult>(json));
        }
        catch (Exception ex) when (TryMapWorkerException(ex, workerTask, activity, ct) is { } problem)
        {
            return problem;
        }
    }

    public async Task<Results<Ok<OcrResult>, ProblemHttpResult>>
        OcrAsync(OcrRequest req, CancellationToken ct)
    {
        var (bytes, imageError) = ValidateAndDecodeImage(req.Image);
        if (imageError is not null) return imageError;

        const WorkerTask workerTask = WorkerTask.Ocr;
        using var activity = StartActivity(workerTask, bytes!.Length, textLength: 0);
        try
        {
            var json = await InvokeWorkerAsync(bytes, req.MediaType, workerTask, textInput: null, activity, ct);
            return TypedResults.Ok(new OcrResult { Text = ExtractResult<string>(json) });
        }
        catch (Exception ex) when (TryMapWorkerException(ex, workerTask, activity, ct) is { } problem)
        {
            return problem;
        }
    }

    public async Task<Results<Ok<OcrRegionsResult>, ProblemHttpResult>>
        OcrRegionsAsync(OcrRequest req, CancellationToken ct)
    {
        var (bytes, imageError) = ValidateAndDecodeImage(req.Image);
        if (imageError is not null) return imageError;

        const WorkerTask workerTask = WorkerTask.OcrWithRegion;
        using var activity = StartActivity(workerTask, bytes!.Length, textLength: 0);
        try
        {
            var json = await InvokeWorkerAsync(bytes, req.MediaType, workerTask, textInput: null, activity, ct);
            return TypedResults.Ok(ExtractResult<OcrRegionsResult>(json));
        }
        catch (Exception ex) when (TryMapWorkerException(ex, workerTask, activity, ct) is { } problem)
        {
            return problem;
        }
    }

    public async Task<Results<Ok<SegmentationResult>, ProblemHttpResult>>
        SegmentAsync(SegmentationRequest req, CancellationToken ct)
    {
        var (bytes, imageError) = ValidateAndDecodeImage(req.Image);
        if (imageError is not null) return imageError;
        if (string.IsNullOrWhiteSpace(req.Text))
            return TypedResults.Problem(detail: "text is required", statusCode: 400);

        const WorkerTask workerTask = WorkerTask.ReferringExpressionSegmentation;
        using var activity = StartActivity(workerTask, bytes!.Length, req.Text.Length);
        try
        {
            var json = await InvokeWorkerAsync(bytes, req.MediaType, workerTask, req.Text, activity, ct);
            return TypedResults.Ok(ExtractResult<SegmentationResult>(json));
        }
        catch (Exception ex) when (TryMapWorkerException(ex, workerTask, activity, ct) is { } problem)
        {
            return problem;
        }
    }

    private (byte[]? bytes, ProblemHttpResult? error) ValidateAndDecodeImage(string? image)
    {
        if (string.IsNullOrWhiteSpace(image))
            return (null, TypedResults.Problem(detail: "image is required", statusCode: 400));

        byte[] decoded;
        try
        {
            decoded = DecodeBase64(image);
        }
        catch (FormatException)
        {
            return (null, TypedResults.Problem(detail: "image is not valid base64", statusCode: 400));
        }

        if (decoded.Length == 0)
            return (null, TypedResults.Problem(detail: "image is empty", statusCode: 400));
        if (decoded.Length > _maxImageBytes)
            return (null, TypedResults.Problem(detail: $"image exceeds {_maxImageBytes} bytes", statusCode: 413));

        return (decoded, null);
    }

    private async Task<string> InvokeWorkerAsync(
        byte[] image,
        string? mediaType,
        WorkerTask workerTask,
        string? textInput,
        Activity? activity,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        using var stream = new MemoryStream(image, writable: false);
        var contentType = mediaType is { Length: > 0 } mt ? mt : "application/octet-stream";
        var json = await _client.RecognizeAsync(stream, contentType, workerTask.ToWireValue(), textInput, ct);
        activity?.SetTag("elapsed_ms", sw.ElapsedMilliseconds);
        return json;
    }

    private TResult ExtractResult<TResult>(string json)
        where TResult : notnull
    {
        var env = JsonSerializer.Deserialize<WorkerEnvelope<TResult>>(json, _json)
            ?? throw new JsonException("worker returned null");
        if (env.Result is null)
            throw new JsonException("worker response missing 'result'");
        return env.Result;
    }

    private ProblemHttpResult? TryMapWorkerException(
        Exception ex, WorkerTask workerTask, Activity? activity, CancellationToken ct)
    {
        switch (ex)
        {
            case HttpRequestException hre when hre.StatusCode == HttpStatusCode.BadRequest:
                activity?.SetStatus(ActivityStatusCode.Error, hre.Message);
                return TypedResults.Problem(detail: hre.Message, statusCode: 400);

            case HttpRequestException hre:
                activity?.SetStatus(ActivityStatusCode.Error, hre.Message);
                _log.LogWarning(hre, "Worker call failed for task {Task}", workerTask);
                return TypedResults.Problem(detail: $"worker error: {hre.Message}", statusCode: 502);

            case TaskCanceledException when !ct.IsCancellationRequested:
                activity?.SetStatus(ActivityStatusCode.Error, "timeout");
                return TypedResults.Problem(detail: "inference timeout", statusCode: 504);

            case JsonException je:
                activity?.SetStatus(ActivityStatusCode.Error, je.Message);
                _log.LogError(je, "Failed to deserialize worker response for task {Task}", workerTask);
                return TypedResults.Problem(detail: "worker returned malformed response", statusCode: 502);

            default:
                return null;
        }
    }

    private static Activity? StartActivity(WorkerTask workerTask, int imageBytes, int textLength)
    {
        var activity = _activitySource.StartActivity("recognize");
        activity?.SetTag("task", workerTask.ToWireValue());
        activity?.SetTag("image.bytes", imageBytes);
        activity?.SetTag("text_input.length", textLength);
        return activity;
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
}
