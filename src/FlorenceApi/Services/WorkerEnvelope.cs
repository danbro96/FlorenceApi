namespace FlorenceApi.Services;

internal sealed class WorkerEnvelope<TResult>
{
    public required TResult Result { get; set; }

    public WorkerImage? Image { get; set; }
}

internal sealed class WorkerImage
{
    public required int Width { get; set; }

    public required int Height { get; set; }
}

internal sealed class WorkerOcrRegionsRaw
{
    public required double[][] QuadBoxes { get; set; }

    public required string[] Labels { get; set; }

    public double[]? Confidence { get; set; }
}
