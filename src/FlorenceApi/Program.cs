using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FlorenceApi.Auth;
using FlorenceApi.Endpoints;
using FlorenceApi.Handlers;
using FlorenceApi.Http;
using FlorenceApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FlorenceOptions>(builder.Configuration.GetSection("Florence"));
builder.Services.Configure<ApiKeyAuthOptions>(builder.Configuration.GetSection("Auth"));

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<FlorenceClient>((sp, http) =>
{
    var opts = sp.GetRequiredService<IOptions<FlorenceOptions>>().Value;
    http.BaseAddress = new Uri(opts.WorkerUrl);
    http.Timeout = TimeSpan.FromSeconds(opts.RequestTimeoutSeconds);
});

builder.Services.AddScoped<RecognitionHandler>();
builder.Services.AddScoped<OptionsHandler>();

// MCP agent surface. The [McpServerToolType] tools in this assembly call the same
// RecognitionHandler as the REST endpoints (no second source of truth). Mounted at /mcp over
// Streamable HTTP, secured by the same X-API-Key scheme (see MapMcp below), and kept
// LAN/WireGuard-only — never published through the Cloudflare Tunnel.
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Liveness (/livez) + readiness (/readyz, pings the inference worker) probes.
builder.Services.AddAppHealthChecks();

builder.Services
    .AddAuthentication(ApiKeyAuthOptions.SchemeName)
    .AddScheme<ApiKeyAuthOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthOptions.SchemeName, opts =>
    {
        var section = builder.Configuration.GetSection("Auth");
        section.Bind(opts);
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var permitsPerMinute = builder.Configuration.GetValue("RateLimit:RequestsPerMinute", 60);
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var key = ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = permitsPerMinute,
            TokensPerPeriod = permitsPerMinute,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    });
});

var allowedOrigins = builder.Configuration.GetSection("Auth:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));
}

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});

var maxImageBytes = builder.Configuration.GetValue("Florence:MaxImageBytes", 8 * 1024 * 1024);

// JSON-with-base64 inflates payload by ~4/3; add headroom for the surrounding JSON envelope.
builder.WebHost.ConfigureKestrel(o =>
    o.Limits.MaxRequestBodySize = (long) (maxImageBytes * 4 / 3) + (128 * 1024));

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, _) =>
    {
        document.Info = new()
        {
            Title = "FlorenceApi",
            Version = "v1",
            Description =
                "Self-hosted image recognition API powered by Microsoft Florence-2 (OpenVINO/Intel Arc). " +
                "All requests are JSON with the image base64-encoded in the body. " +
                "Authenticate by sending your key in the `X-API-Key` header.",
        };
        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyAuthOptions.HeaderName,
            Description = "API key. Send in the X-API-Key header.",
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        var endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
        var requiresAuth = endpointMetadata.OfType<IAuthorizeData>().Any()
                        && !endpointMetadata.OfType<IAllowAnonymous>().Any();
        if (requiresAuth)
        {
            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("ApiKey", context.Document)] = new List<string>(),
            });
        }

        // The cross-cutting code no endpoint declares — ProblemExceptionHandler produces it.

        AddProblem(operation, context.Document, StatusCodes.Status500InternalServerError, "Internal server error");

        // Bodyless 4xx/5xx come from the non-generic arms of the typed-result unions (NotFound,

        // UnauthorizedHttpResult). UseStatusCodePages fills them at runtime, so declare the shape.

        foreach (var code in operation.Responses?.Keys.ToList() ?? [])
        {
            if (code.Length != 3 || code[0] is not ('4' or '5')) continue;

            var existing = operation.Responses![code];

            if (existing.Content is { Count: > 0 }) continue;

            operation.Responses[code] = new OpenApiResponse
            { Description = existing.Description, Content = ProblemContent(context.Document) };
        }

        return Task.CompletedTask;
    });
});

// Every error response carries the same shape, so a generated client types its error once instead of
// falling back to `void`.
static Dictionary<string, OpenApiMediaType> ProblemContent(OpenApiDocument document) =>
    new() { ["application/problem+json"] = new() { Schema = new OpenApiSchemaReference("ProblemDetails", document) } };

static void AddProblem(OpenApiOperation operation, OpenApiDocument document, int status, string description)
{
    var code = status.ToString(CultureInfo.InvariantCulture);
    operation.Responses ??= [];
    if (operation.Responses.ContainsKey(code)) return;
    operation.Responses[code] = new OpenApiResponse { Description = description, Content = ProblemContent(document) };
}

// RFC 9457. Declared here because nothing returns the CLR type directly, so the generator never emits it.
static OpenApiSchema ProblemDetailsSchema() => new()
{
    Type = JsonSchemaType.Object,
    Description = "RFC 9457 problem details.",
    Properties = new Dictionary<string, IOpenApiSchema>
    {
        ["type"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
        ["title"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
        ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.Null, Format = "int32" },
        ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
        ["instance"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
        ["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
    },
};

var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(
            serviceName: "florence-api",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
        .WithTracing(t => t
            .AddSource("FlorenceApi.Recognize")
            .AddAspNetCoreInstrumentation(o =>
            {
                o.RecordException = true;
                // Health probes are polled constantly by docker + devops-monitor; their spans add nothing.
                o.Filter = ctx => ctx.Request.Path != "/livez" && ctx.Request.Path != "/readyz";
            })
            .AddHttpClientInstrumentation()
            .AddOtlpExporter())
        .WithMetrics(m => m
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter());

    builder.Logging.AddOpenTelemetry(o =>
    {
        o.IncludeFormattedMessage = true;
        o.IncludeScopes = true;
        o.AddOtlpExporter();
    });
}

var app = builder.Build();

app.UseStaticFiles();
if (allowedOrigins.Length > 0) app.UseCors();
app.UseExceptionHandler();
// Fills the empty body of a bare 4xx (auth challenges, TypedResults.NotFound) with
// ProblemDetails, so the spec's promise holds. Scoped away from /mcp — JSON-RPC has its own error shape.
app.UseWhen(c => !c.Request.Path.StartsWithSegments("/mcp"), b => b.UseStatusCodePages());

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Keep the MCP surface LAN/WireGuard-only: 404 any /mcp request that arrived via the
// Cloudflare Tunnel (backstop behind the tunnel ingress not routing /mcp at all).
app.UseMcpLanOnly();

app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
app.MapScalarApiReference("/scalar", o => o
        .WithTitle("FlorenceApi")
        .WithTheme(ScalarTheme.BluePlanet))
    .AllowAnonymous();

app.MapAppHealthChecks(app.Environment);
app.MapOptionsEndpoint().RequireAuthorization();

app.MapCaptions().RequireAuthorization();
app.MapDetections().RequireAuthorization();
app.MapGrounding().RequireAuthorization();
app.MapOcr().RequireAuthorization();
app.MapOcrRegions().RequireAuthorization();
app.MapSegmentations().RequireAuthorization();

// Agent MCP surface (Streamable HTTP). Mapped AFTER UseAuthentication/UseAuthorization so the
// same X-API-Key scheme validates it; RequireAuthorization rejects anonymous calls with 401.
// Exposure is LAN/WireGuard-only — the Cloudflare Tunnel must not route /mcp (see deploy notes).
app.MapMcp("/mcp").RequireAuthorization();

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program;
