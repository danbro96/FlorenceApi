namespace FlorenceApi.Models;

public sealed class OptionsResponse
{
    public required string Model { get; set; }

    public required string Revision { get; set; }

    public required string Device { get; set; }

    public required int MaxImageBytes { get; set; }

    public required string[] SupportedFormats { get; set; }
}
