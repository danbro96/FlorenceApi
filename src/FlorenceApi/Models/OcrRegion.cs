namespace FlorenceApi.Models;

public sealed class OcrRegion
{
    public required string Text { get; set; }

    /// <summary>
    /// 8-element polygon as <c>[x1,y1,x2,y2,x3,y3,x4,y4]</c>, ordered TL, TR, BR, BL.
    /// Native shape from Florence-2; preserved verbatim for clients that need rotated polygons.
    /// </summary>
    public required double[] Quad { get; set; }

    /// <summary>Axis-aligned bounding box derived from <see cref="Quad"/>.</summary>
    public required BoundingBox Box { get; set; }

    /// <summary>
    /// Region rotation in degrees, derived from the top edge of <see cref="Quad"/> via
    /// <c>atan2(y2-y1, x2-x1)</c>. Range <c>(-180, 180]</c>; ~0 for upright text, ~±180 for upside-down.
    /// </summary>
    public required double Rotation { get; set; }

    /// <summary>
    /// Mean per-token probability of this region's label tokens, in <c>[0, 1]</c>.
    /// Relative ranking signal — not a calibrated probability.
    /// </summary>
    public required double Confidence { get; set; }
}
