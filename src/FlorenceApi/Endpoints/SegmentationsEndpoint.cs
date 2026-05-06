using FlorenceApi.Handlers;
using FlorenceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlorenceApi.Endpoints;

public static class SegmentationsEndpoint
{
    public static IEndpointConventionBuilder MapSegmentations(this IEndpointRouteBuilder app) =>
        app.MapPost("/segmentations", (
                [FromBody] SegmentationRequest req,
                RecognitionHandler h,
                CancellationToken ct) => h.SegmentAsync(req, ct))
            .WithTags("Segmentations")
            .WithSummary("Segment a region described by free text.")
            .WithDescription(
                """
                Given an image and a free-text expression (in `text`), returns one or more polygons
                covering the matching region(s) plus labels.

                `polygons` is `polygons[i][j]` = polygon `j` of region `i`, each polygon a flat
                `[x1, y1, x2, y2, ...]` array of pixel coordinates. A region may consist of multiple
                disjoint polygons (e.g. an object split by occlusion).

                Maps to Florence-2 task `<REFERRING_EXPRESSION_SEGMENTATION>`.
                """)
            .Accepts<SegmentationRequest>("application/json")
            .Produces<SegmentationResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
}
