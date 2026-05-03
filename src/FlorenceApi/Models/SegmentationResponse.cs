namespace FlorenceApi.Models;

public sealed class SegmentationResponse
{
    public required string Task { get; set; }

    public required ImageSize Image { get; set; }

    public required SegmentationResult Result { get; set; }

    public required long ElapsedMs { get; set; }
}
