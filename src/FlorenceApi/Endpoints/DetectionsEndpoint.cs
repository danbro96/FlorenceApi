using FlorenceApi.Handlers;
using FlorenceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlorenceApi.Endpoints;

public static class DetectionsEndpoint
{
    public static IEndpointConventionBuilder MapDetections(this IEndpointRouteBuilder app) =>
        app.MapPost("/detections", (
                [FromBody] DetectionRequest req,
                RecognitionHandler h,
                CancellationToken ct) => h.DetectAsync(req, ct))
            .WithTags("Detections")
            .WithSummary("Detect objects or regions in an image.")
            .WithDescription(
                """
                Returns axis-aligned bounding boxes (`[x1, y1, x2, y2]` in pixel coords) and labels.

                The `variant` field selects the detection mode:
                - `od` (default) — object detection over the COCO-style label set.
                - `dense` — dense region captioning; one label per region with descriptive captions.
                - `proposal` — class-agnostic region proposals with empty labels.

                Maps to Florence-2 tasks `<OD>`, `<DENSE_REGION_CAPTION>`, `<REGION_PROPOSAL>`.
                """)
            .Accepts<DetectionRequest>("application/json")
            .Produces<DetectionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
}
