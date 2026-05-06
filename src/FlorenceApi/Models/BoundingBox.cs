namespace FlorenceApi.Models;

public sealed class BoundingBox
{
    public required double XMin { get; set; }

    public required double YMin { get; set; }

    public required double XMax { get; set; }

    public required double YMax { get; set; }
}
