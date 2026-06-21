using System.Net;
using Xunit;

namespace FlorenceApi.IntegrationTests;

/// <summary>Boot smoke: the .NET app starts in-process, serves health + OpenAPI anonymously, and gates the
/// recognition surface behind the X-API-Key scheme.</summary>
public sealed class SmokeTests(FlorenceApiTestFactory factory) : IClassFixture<FlorenceApiTestFactory>
{
    [Fact]
    public async Task Livez_is_ok()
    {
        var resp = await factory.CreateClient().GetAsync("/livez");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        var resp = await factory.CreateClient().GetAsync("/openapi/v1.json");
        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Options_without_an_api_key_is_unauthorized()
    {
        var resp = await factory.CreateClient().GetAsync("/options");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
