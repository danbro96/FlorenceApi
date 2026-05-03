using FlorenceApi.Handlers;
using FlorenceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlorenceApi.Endpoints;

public static class CaptionsEndpoint
{
    public static IEndpointConventionBuilder MapCaptions(this IEndpointRouteBuilder app) =>
        app.MapPost("/captions", (
                [FromBody] CaptionRequest req,
                RecognitionHandler h,
                CancellationToken ct) => h.CaptionAsync(req, ct))
            .WithTags("Captions")
            .WithSummary("Generate a natural-language caption for an image.")
            .WithDescription(
                """
                Returns a single caption string describing the image content.

                The `detail` field selects verbosity:
                - `short` (default) — a brief one-sentence caption.
                - `detailed` — a longer descriptive caption with more visual detail.
                - `more_detailed` — a multi-sentence caption with fine-grained description.

                Maps to Florence-2 tasks `<CAPTION>`, `<DETAILED_CAPTION>`, `<MORE_DETAILED_CAPTION>`.
                """);
}
