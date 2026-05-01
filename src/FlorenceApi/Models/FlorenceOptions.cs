namespace FlorenceApi.Models;

public sealed class FlorenceOptions
{
    public string WorkerUrl { get; set; } = "http://localhost:9000";
    public int MaxImageBytes { get; set; } = 8 * 1024 * 1024;
    public int RequestTimeoutSeconds { get; set; } = 60;
}
