using FlorenceApi.Models.Enums;

namespace FlorenceApi.Models;

public sealed class CaptionRequest : ImageRequest
{
    public CaptionDetail Detail { get; set; } = CaptionDetail.Short;
}
