namespace FlorenceApi.Models;

public enum CaptionDetail { Short, Detailed, MoreDetailed }
public enum DetectionVariant { Od, Dense, Proposal }

public abstract class ImageRequest
{
    public required string Image { get; set; }
    public string? MediaType { get; set; }
}

public sealed class CaptionRequest : ImageRequest
{
    public CaptionDetail Detail { get; set; } = CaptionDetail.Short;
}

public sealed class DetectionRequest : ImageRequest
{
    public DetectionVariant Variant { get; set; } = DetectionVariant.Od;
}

public sealed class GroundingRequest : ImageRequest
{
    public required string Text { get; set; }
}

public sealed class OcrRequest : ImageRequest { }

public sealed class SegmentationRequest : ImageRequest
{
    public required string Text { get; set; }
}

public sealed class ImageSize
{
    public required int Width { get; set; }
    public required int Height { get; set; }
}

public sealed class CaptionResponse
{
    public required string Task { get; set; }
    public required ImageSize Image { get; set; }
    public required string Result { get; set; }
    public required long ElapsedMs { get; set; }
}

public sealed class DetectionResult
{
    public required double[][] Bboxes { get; set; }
    public required string[] Labels { get; set; }
}

public sealed class DetectionResponse
{
    public required string Task { get; set; }
    public required ImageSize Image { get; set; }
    public required DetectionResult Result { get; set; }
    public required long ElapsedMs { get; set; }
}

public sealed class OcrRegionsResult
{
    public required double[][] QuadBoxes { get; set; }
    public required string[] Labels { get; set; }
}

public sealed class OcrRegionsResponse
{
    public required string Task { get; set; }
    public required ImageSize Image { get; set; }
    public required OcrRegionsResult Result { get; set; }
    public required long ElapsedMs { get; set; }
}

public sealed class SegmentationResult
{
    public required double[][][] Polygons { get; set; }
    public required string[] Labels { get; set; }
}

public sealed class SegmentationResponse
{
    public required string Task { get; set; }
    public required ImageSize Image { get; set; }
    public required SegmentationResult Result { get; set; }
    public required long ElapsedMs { get; set; }
}

public sealed class OptionsResponse
{
    public required string Model { get; set; }
    public required string Revision { get; set; }
    public required string Device { get; set; }
    public required int MaxImageBytes { get; set; }
    public required string[] SupportedFormats { get; set; }
}

public sealed class HealthResponse
{
    public required string Status { get; set; }
}
