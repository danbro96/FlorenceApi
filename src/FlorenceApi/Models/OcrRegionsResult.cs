namespace FlorenceApi.Models;

public sealed class OcrRegionsResult
{
    /// <summary>Recognized text regions, sorted top-to-bottom and left-to-right.</summary>
    public required IReadOnlyList<OcrRegion> Regions { get; set; }

    /// <summary>Pixel dimensions of the image the regions are expressed in.</summary>
    public required ImageSize Image { get; set; }
}
