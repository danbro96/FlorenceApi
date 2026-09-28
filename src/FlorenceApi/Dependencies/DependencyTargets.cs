using FlorenceApi.Services;

namespace FlorenceApi.Dependencies;

/// <summary>Roster derived from the same options the real clients bind — edges cannot drift.</summary>
public static class DependencyTargets
{
    public static IReadOnlyList<DependencyTarget> From(FlorenceOptions florence) =>
    [
        new DependencyTarget
        {
            Name = "florence-worker",
            BaseUrl = florence.WorkerUrl,
            ProbePath = "readyz",
        },
    ];
}
