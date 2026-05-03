namespace FlorenceApi.Models;

public abstract class ImageRequest
{
    public required string Image { get; set; }

    public string? MediaType { get; set; }
}
