namespace FlorenceApi.Models;

public sealed class OcrRegionsResponse
{
    public required string Task { get; set; }

    public required ImageSize Image { get; set; }

    public required OcrRegionsResult Result { get; set; }

    public required long ElapsedMs { get; set; }
}
