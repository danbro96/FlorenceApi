namespace FlorenceApi.Dependencies;

/// <summary>One outward edge; the worker is unauthenticated, as the real client calls it.</summary>
public sealed class DependencyTarget
{
    public required string Name { get; set; }

    public required string BaseUrl { get; set; }

    public required string ProbePath { get; set; }
}
