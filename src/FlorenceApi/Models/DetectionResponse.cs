namespace FlorenceApi.Models;

public sealed class DetectionResponse
{
    public required string Task { get; set; }

    public required ImageSize Image { get; set; }

    public required DetectionResult Result { get; set; }

    public required long ElapsedMs { get; set; }
}
