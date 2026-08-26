using FlorenceApi.Dtos.Enums;

namespace FlorenceApi.Dtos;

public sealed class CaptionRequest : ImageRequest
{
    public CaptionDetail Detail { get; set; } = CaptionDetail.Short;
}
