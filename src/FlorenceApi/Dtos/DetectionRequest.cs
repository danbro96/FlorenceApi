using FlorenceApi.Dtos.Enums;

namespace FlorenceApi.Dtos;

public sealed class DetectionRequest : ImageRequest
{
    public DetectionVariant Variant { get; set; } = DetectionVariant.Od;
}
