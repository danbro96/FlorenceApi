namespace FlorenceApi.Models;

public sealed class SegmentationResult
{
    public required double[][][] Polygons { get; set; }

    public required string[] Labels { get; set; }
}
