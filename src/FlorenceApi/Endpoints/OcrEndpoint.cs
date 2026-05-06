using FlorenceApi.Handlers;
using FlorenceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlorenceApi.Endpoints;

public static class OcrEndpoint
{
    public static IEndpointConventionBuilder MapOcr(this IEndpointRouteBuilder app) =>
        app.MapPost("/ocr", (
                [FromBody] OcrRequest req,
                RecognitionHandler h,
                CancellationToken ct) => h.OcrAsync(req, ct))
            .WithTags("OCR")
            .WithSummary("Extract text from an image (no regions).")
            .WithDescription(
                """
                Returns the recognized text as a single string. No coordinates.

                Use `/ocr/regions` if you need per-text-block bounding boxes.

                Maps to Florence-2 task `<OCR>`.
                """)
            .Accepts<OcrRequest>("application/json")
            .Produces<OcrResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

    public static IEndpointConventionBuilder MapOcrRegions(this IEndpointRouteBuilder app) =>
        app.MapPost("/ocr/regions", (
                [FromBody] OcrRequest req,
                RecognitionHandler h,
                CancellationToken ct) => h.OcrRegionsAsync(req, ct))
            .WithTags("OCR")
            .WithSummary("Extract text from an image with per-region polygons, bounding boxes, rotation, and confidence.")
            .WithDescription(
                """
                Returns a list of recognized text regions plus the image dimensions the regions are
                expressed in. Regions are sorted top-to-bottom, left-to-right.

                Each region carries:

                - `text` — recognized text.
                - `quad` — 8-element polygon `[x1,y1,x2,y2,x3,y3,x4,y4]`, ordered TL, TR, BR, BL.
                  Native shape from Florence-2; preserved verbatim for clients that need rotated polygons.
                - `box` — axis-aligned bounding box derived from `quad` (server-side).
                - `rotation` — degrees, derived from the top edge of `quad` via `atan2(y2-y1, x2-x1)`.
                  Range `(-180, 180]`; ~0 for upright text, ~±180 for upside-down. Florence-2 does not
                  expose a rotation field natively — this is computed from the quad geometry.
                - `confidence` — mean per-token probability of the region's label tokens, in `[0, 1]`.
                  Useful as a relative ranking signal; not a calibrated probability.

                Maps to Florence-2 task `<OCR_WITH_REGION>`.
                """)
            .Accepts<OcrRequest>("application/json")
            .Produces<OcrRegionsResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
}
