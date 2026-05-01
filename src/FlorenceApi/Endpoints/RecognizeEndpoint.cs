using System.Diagnostics;
using FlorenceApi.Services;

namespace FlorenceApi.Endpoints;

public static class RecognizeEndpoint
{
    static readonly ActivitySource Activity = new("FlorenceApi.Recognize");

    public static IEndpointConventionBuilder MapRecognizeEndpoint(this IEndpointRouteBuilder app)
    {
        return app.MapPost("/recognize", async (
            HttpRequest request,
            FlorenceClient client,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "multipart/form-data required" });

            var form = await request.ReadFormAsync(ct);
            var image = form.Files["image"];
            var task = form["task"].ToString();
            var textInput = form["text_input"].ToString();

            if (image is null || image.Length == 0)
                return Results.BadRequest(new { error = "image file is required" });
            if (string.IsNullOrWhiteSpace(task))
                return Results.BadRequest(new { error = "task is required" });

            using var activity = Activity.StartActivity("recognize");
            activity?.SetTag("task", task);
            activity?.SetTag("image.bytes", image.Length);
            activity?.SetTag("text_input.length", textInput.Length);

            var sw = Stopwatch.StartNew();
            try
            {
                await using var stream = image.OpenReadStream();
                var contentType = string.IsNullOrEmpty(image.ContentType)
                    ? "application/octet-stream"
                    : image.ContentType;
                var json = await client.RecognizeAsync(
                    stream,
                    contentType,
                    task,
                    string.IsNullOrWhiteSpace(textInput) ? null : textInput,
                    ct);
                activity?.SetTag("elapsed_ms", sw.ElapsedMilliseconds);
                return Results.Content(json, "application/json");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                return Results.Problem($"Worker error: {ex.Message}", statusCode: 502);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "timeout");
                return Results.Problem("Inference timeout.", statusCode: 504);
            }
        });
    }
}
