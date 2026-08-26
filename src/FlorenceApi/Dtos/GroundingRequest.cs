namespace FlorenceApi.Dtos;

public sealed class GroundingRequest : ImageRequest
{
    public required string Text { get; set; }
}
