namespace FlorenceApi.Services;

internal enum WorkerTask
{
    Caption,
    DetailedCaption,
    MoreDetailedCaption,
    Od,
    DenseRegionCaption,
    RegionProposal,
    CaptionToPhraseGrounding,
    Ocr,
    OcrWithRegion,
    ReferringExpressionSegmentation,
}

internal static class WorkerTaskExtensions
{
    // Single source of truth for the snake_case vocabulary the Python worker accepts.
    // Worker-side renames land here and nowhere else.
    public static string ToWireValue(this WorkerTask task) => task switch
    {
        WorkerTask.Caption => "caption",
        WorkerTask.DetailedCaption => "detailed_caption",
        WorkerTask.MoreDetailedCaption => "more_detailed_caption",
        WorkerTask.Od => "od",
        WorkerTask.DenseRegionCaption => "dense_region_caption",
        WorkerTask.RegionProposal => "region_proposal",
        WorkerTask.CaptionToPhraseGrounding => "caption_to_phrase_grounding",
        WorkerTask.Ocr => "ocr",
        WorkerTask.OcrWithRegion => "ocr_with_region",
        WorkerTask.ReferringExpressionSegmentation => "referring_expression_segmentation",
        _ => throw new ArgumentOutOfRangeException(nameof(task), task, null),
    };
}
