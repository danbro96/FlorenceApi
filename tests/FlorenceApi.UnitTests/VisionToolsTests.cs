using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlorenceApi.Handlers;
using FlorenceApi.Mcp;
using FlorenceApi.Models;
using FlorenceApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using Xunit;

namespace FlorenceApi.UnitTests;

/// <summary>
/// The MCP vision surface delegates to the same <see cref="RecognitionHandler"/> as REST and
/// unwraps its result union: the typed value on success, an <see cref="McpException"/> on a worker
/// problem. The worker is stubbed, so these stay fast and offline.
/// </summary>
public class VisionToolsTests
{
    private static readonly string Image = Convert.ToBase64String(Encoding.UTF8.GetBytes("fake-image-bytes"));

    private static VisionTools Tools(HttpStatusCode status, string body)
    {
        var http = new HttpClient(new StubHandler(status, body)) { BaseAddress = new Uri("http://worker.local") };
        var json = new Microsoft.AspNetCore.Http.Json.JsonOptions();
        json.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        json.SerializerOptions.PropertyNameCaseInsensitive = true;
        json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        var handler = new RecognitionHandler(
            new FlorenceClient(http),
            NullLogger<RecognitionHandler>.Instance,
            Options.Create(new FlorenceOptions { MaxImageBytes = 8 * 1024 * 1024 }),
            Options.Create(json));
        return new VisionTools(handler);
    }

    [Fact]
    public async Task Caption_returns_the_unwrapped_worker_result()
    {
        var tools = Tools(HttpStatusCode.OK, """{"result":"a cat on a sofa"}""");

        var result = await tools.Caption(Image);

        Assert.Equal("a cat on a sofa", result.Caption);
    }

    [Fact]
    public async Task Worker_error_surfaces_as_an_McpException()
    {
        var tools = Tools(HttpStatusCode.BadRequest, "the worker rejected the image");

        var ex = await Assert.ThrowsAsync<McpException>(() => tools.Caption(Image));

        Assert.Contains("rejected", ex.Message);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}
