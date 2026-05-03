using FlorenceApi.Models.Enums;

namespace FlorenceApi.Models;

public sealed class DetectionRequest : ImageRequest
{
    public DetectionVariant Variant { get; set; } = DetectionVariant.Od;
}
