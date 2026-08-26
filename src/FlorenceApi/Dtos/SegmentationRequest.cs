namespace FlorenceApi.Dtos;

public sealed class SegmentationRequest : ImageRequest
{
    public required string Text { get; set; }
}
