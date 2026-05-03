namespace FlorenceApi.Models;

public sealed class DetectionResult
{
    public required double[][] Bboxes { get; set; }

    public required string[] Labels { get; set; }
}
