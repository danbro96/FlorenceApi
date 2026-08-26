namespace FlorenceApi.Services;

internal sealed class WorkerOcrRegionsRaw
{
    public required double[][] QuadBoxes { get; set; }

    public required string[] Labels { get; set; }

    public double[]? Confidence { get; set; }
}
