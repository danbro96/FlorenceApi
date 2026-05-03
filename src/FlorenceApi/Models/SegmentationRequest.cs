namespace FlorenceApi.Models;

public sealed class SegmentationRequest : ImageRequest
{
    public required string Text { get; set; }
}
