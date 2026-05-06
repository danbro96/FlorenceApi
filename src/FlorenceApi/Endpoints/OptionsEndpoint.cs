using FlorenceApi.Handlers;
using FlorenceApi.Models;

namespace FlorenceApi.Endpoints;

public static class OptionsEndpoint
{
    public static IEndpointConventionBuilder MapOptionsEndpoint(this IEndpointRouteBuilder app) =>
        app.MapGet("/options", (
                OptionsHandler h,
                CancellationToken ct) => h.GetAsync(ct))
            .WithTags("Meta")
            .WithSummary("Runtime options reported by the worker (model, device, limits).")
            .WithDescription(
                """
                Returns the model id and Hugging Face revision currently loaded, the OpenVINO device
                in use (`GPU`, `GPU.0`, `CPU`, …), the max image upload size, and the accepted image
                formats. The list of recognition tasks lives in the OpenAPI spec at `/openapi/v1.json`
                — fetch that for client codegen.
                """)
            .Produces<OptionsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
}
