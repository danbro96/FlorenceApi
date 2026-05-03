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
                """);

    public static IEndpointConventionBuilder MapOcrRegions(this IEndpointRouteBuilder app) =>
        app.MapPost("/ocr/regions", (
                [FromBody] OcrRequest req,
                RecognitionHandler h,
                CancellationToken ct) => h.OcrRegionsAsync(req, ct))
            .WithTags("OCR")
            .WithSummary("Extract text from an image with per-block region polygons.")
            .WithDescription(
                """
                Returns a parallel list of `quad_boxes` (8-point polygons:
                `[x1, y1, x2, y2, x3, y3, x4, y4]`, NOT axis-aligned bounding boxes) and `labels`
                (the recognized text per region).

                Quad boxes are the native shape for rotated/curved text in Florence-2 OCR; convert to
                axis-aligned boxes client-side if your renderer requires that.

                Maps to Florence-2 task `<OCR_WITH_REGION>`.
                """);
}
