using FlorenceApi.Handlers;
using FlorenceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlorenceApi.Endpoints;

public static class GroundingEndpoint
{
    public static IEndpointConventionBuilder MapGrounding(this IEndpointRouteBuilder app) =>
        app.MapPost("/grounding", (
                [FromBody] GroundingRequest req,
                RecognitionHandler h,
                CancellationToken ct) => h.GroundAsync(req, ct))
            .WithTags("Grounding")
            .WithSummary("Locate phrases from a caption inside an image.")
            .WithDescription(
                """
                Given an image and a free-text caption (in `text`), returns bounding boxes for the noun
                phrases mentioned in that caption. Each label is the matched phrase; each box is its
                corresponding region.

                Example: `text = "a cat on a sofa"` returns boxes for "a cat" and "a sofa".

                Maps to Florence-2 task `<CAPTION_TO_PHRASE_GROUNDING>`.
                """);
}
