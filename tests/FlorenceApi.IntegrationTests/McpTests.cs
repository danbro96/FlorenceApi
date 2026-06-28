using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FlorenceApi.IntegrationTests;

/// <summary>The agent MCP surface is mounted and gated by the same X-API-Key scheme as the REST endpoints.</summary>
public sealed class McpTests(FlorenceApiTestFactory factory) : IClassFixture<FlorenceApiTestFactory>
{
    [Fact]
    public async Task Mcp_without_an_api_key_is_unauthorized()
    {
        var resp = await factory.CreateClient().PostAsJsonAsync("/mcp", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
