namespace FlorenceApi.Models;

public sealed class GroundingRequest : ImageRequest
{
    public required string Text { get; set; }
}
