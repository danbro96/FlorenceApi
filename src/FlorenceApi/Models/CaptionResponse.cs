namespace FlorenceApi.Models;

public sealed class CaptionResponse
{
    public required string Task { get; set; }

    public required ImageSize Image { get; set; }

    public required string Result { get; set; }

    public required long ElapsedMs { get; set; }
}
