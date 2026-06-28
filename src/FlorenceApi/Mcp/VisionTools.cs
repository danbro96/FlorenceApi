using FlorenceApi.Handlers;
using FlorenceApi.Models;
using FlorenceApi.Models.Enums;
using Microsoft.AspNetCore.Http.HttpResults;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace FlorenceApi.Mcp;

/// <summary>
/// The agent's MCP surface for Florence-2 vision. Each tool calls the SAME
/// <see cref="RecognitionHandler"/> as the REST endpoints (no second source of truth) and unwraps
/// its <c>Results&lt;Ok&lt;T&gt;, ProblemHttpResult&gt;</c> — success returns the typed result, a
/// problem surfaces to the agent as an <see cref="McpException"/>. Images are passed as base64
/// (optionally a <c>data:</c> URL), the same shape the REST endpoints accept.
/// </summary>
[McpServerToolType]
public sealed class VisionTools
{
    private readonly RecognitionHandler _handler;

    public VisionTools(RecognitionHandler handler) => _handler = handler;

    [McpServerTool(Name = "ocr")]
    [Description("Extract all readable text from an image as plain text, in reading order.")]
    public Task<OcrResult> Ocr(
        [Description("The image as base64 (a bare base64 string or a data: URL).")] string image,
        [Description("Image media type, e.g. image/png (optional).")] string? mediaType = null,
        CancellationToken ct = default) =>
        Unwrap(_handler.OcrAsync(new OcrRequest { Image = image, MediaType = mediaType }, ct));

    [McpServerTool(Name = "ocr_regions")]
    [Description("Extract text with per-region geometry: rotated quad, bounding box, rotation degrees, and confidence.")]
    public Task<OcrRegionsResult> OcrRegions(
        [Description("The image as base64 (a bare base64 string or a data: URL).")] string image,
        [Description("Image media type, e.g. image/png (optional).")] string? mediaType = null,
        CancellationToken ct = default) =>
        Unwrap(_handler.OcrRegionsAsync(new OcrRequest { Image = image, MediaType = mediaType }, ct));

    [McpServerTool(Name = "caption")]
    [Description("Describe an image in natural language. detail = Short, Detailed, or MoreDetailed.")]
    public Task<CaptionResult> Caption(
        [Description("The image as base64 (a bare base64 string or a data: URL).")] string image,
        [Description("Caption verbosity: Short, Detailed, or MoreDetailed (default Short).")] CaptionDetail detail = CaptionDetail.Short,
        [Description("Image media type, e.g. image/png (optional).")] string? mediaType = null,
        CancellationToken ct = default) =>
        Unwrap(_handler.CaptionAsync(new CaptionRequest { Image = image, MediaType = mediaType, Detail = detail }, ct));

    [McpServerTool(Name = "detect")]
    [Description("Detect objects and return boxes + labels. variant = Od (COCO objects), Dense (dense region captions), or Proposal (class-agnostic regions).")]
    public Task<DetectionResult> Detect(
        [Description("The image as base64 (a bare base64 string or a data: URL).")] string image,
        [Description("Detection variant: Od, Dense, or Proposal (default Od).")] DetectionVariant variant = DetectionVariant.Od,
        [Description("Image media type, e.g. image/png (optional).")] string? mediaType = null,
        CancellationToken ct = default) =>
        Unwrap(_handler.DetectAsync(new DetectionRequest { Image = image, MediaType = mediaType, Variant = variant }, ct));

    [McpServerTool(Name = "ground")]
    [Description("Locate the noun phrases of a caption in the image (phrase grounding): returns a box per phrase.")]
    public Task<DetectionResult> Ground(
        [Description("The image as base64 (a bare base64 string or a data: URL).")] string image,
        [Description("Caption text whose phrases should be located, e.g. 'a cat on a sofa'.")] string text,
        [Description("Image media type, e.g. image/png (optional).")] string? mediaType = null,
        CancellationToken ct = default) =>
        Unwrap(_handler.GroundAsync(new GroundingRequest { Image = image, MediaType = mediaType, Text = text }, ct));

    [McpServerTool(Name = "segment")]
    [Description("Segment the region matching a free-form phrase (referring-expression segmentation): returns polygon masks.")]
    public Task<SegmentationResult> Segment(
        [Description("The image as base64 (a bare base64 string or a data: URL).")] string image,
        [Description("The phrase describing what to segment, e.g. 'the red car'.")] string text,
        [Description("Image media type, e.g. image/png (optional).")] string? mediaType = null,
        CancellationToken ct = default) =>
        Unwrap(_handler.SegmentAsync(new SegmentationRequest { Image = image, MediaType = mediaType, Text = text }, ct));

    /// <summary>Unwrap a handler's typed union: the value on success, an <see cref="McpException"/> on a problem.</summary>
    private static async Task<T> Unwrap<T>(Task<Results<Ok<T>, ProblemHttpResult>> call)
        where T : notnull
    {
        var result = await call;
        return ((INestedHttpResult)result).Result switch
        {
            Ok<T> ok => ok.Value!,
            ProblemHttpResult problem => throw new McpException(problem.ProblemDetails.Detail ?? "vision request failed"),
            _ => throw new McpException("unexpected vision result"),
        };
    }
}
