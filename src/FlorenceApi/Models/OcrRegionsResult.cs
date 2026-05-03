namespace FlorenceApi.Models;

public sealed class OcrRegionsResult
{
    public required double[][] QuadBoxes { get; set; }

    public required string[] Labels { get; set; }
}
