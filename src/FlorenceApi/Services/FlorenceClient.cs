using System.Net.Http.Headers;

namespace FlorenceApi.Services;

public sealed class FlorenceClient
{
    readonly HttpClient _http;

    public FlorenceClient(HttpClient http) => _http = http;

    public async Task<string> GetOptionsAsync(CancellationToken ct)
    {
        using var resp = await _http.GetAsync("/options", ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    public async Task<string> RecognizeAsync(
        Stream imageStream,
        string contentType,
        string task,
        string? textInput,
        CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        var imageContent = new StreamContent(imageStream);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        content.Add(imageContent, "image", "image");
        content.Add(new StringContent(task), "task");
        if (!string.IsNullOrEmpty(textInput))
            content.Add(new StringContent(textInput), "text_input");

        using var resp = await _http.PostAsync("/recognize", content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(body, inner: null, statusCode: resp.StatusCode);
        return body;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            using var resp = await _http.GetAsync("/healthz", cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
