using FlorenceApi.Dependencies;
using FlorenceApi.Services;
using Xunit;

namespace FlorenceApi.UnitTests;

public class DependencyTargetsTests
{
    [Fact]
    public void Worker_edge_uses_the_registry_name_and_the_client_worker_url()
    {
        var target = Assert.Single(DependencyTargets.From(new FlorenceOptions { WorkerUrl = "http://worker:9000" }));

        Assert.Equal("florence-worker", target.Name);
        Assert.Equal("http://worker:9000", target.BaseUrl);
    }

    [Fact]
    public void Worker_edge_probes_the_same_readiness_path_as_readyz()
    {
        var target = Assert.Single(DependencyTargets.From(new FlorenceOptions()));

        Assert.Equal("readyz", target.ProbePath);
    }

    [Fact]
    public void Blank_worker_url_flows_through_so_the_probe_reports_unconfigured()
    {
        var target = Assert.Single(DependencyTargets.From(new FlorenceOptions { WorkerUrl = string.Empty }));

        Assert.Equal(string.Empty, target.BaseUrl);
    }
}
