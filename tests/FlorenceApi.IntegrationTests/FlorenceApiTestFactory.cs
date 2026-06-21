using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlorenceApi.IntegrationTests;

/// <summary>Hosts the real .NET app in-process. The Python worker is reached lazily over HttpClient and is never
/// called by these smoke tests (they assert boot, OpenAPI, and the auth gate only), so no stub is required.</summary>
public sealed class FlorenceApiTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");
}
