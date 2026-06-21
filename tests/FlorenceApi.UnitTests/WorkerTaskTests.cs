using FlorenceApi.Services;
using Xunit;

namespace FlorenceApi.UnitTests;

/// <summary>The wire vocabulary the Python worker accepts — the single source of truth mapping enum → token.
/// A drift here silently breaks every /recognize call, so it's worth pinning.</summary>
public class WorkerTaskTests
{
    // WorkerTask is internal (exposed via InternalsVisibleTo), so it can't appear in a public [Theory]
    // parameter list — the assertions live in the method body instead.
    [Fact]
    public void Maps_each_task_to_its_worker_token()
    {
        Assert.Equal("caption", WorkerTask.Caption.ToWireValue());
        Assert.Equal("detailed_caption", WorkerTask.DetailedCaption.ToWireValue());
        Assert.Equal("more_detailed_caption", WorkerTask.MoreDetailedCaption.ToWireValue());
        Assert.Equal("od", WorkerTask.Od.ToWireValue());
        Assert.Equal("caption_to_phrase_grounding", WorkerTask.CaptionToPhraseGrounding.ToWireValue());
        Assert.Equal("ocr", WorkerTask.Ocr.ToWireValue());
        Assert.Equal("ocr_with_region", WorkerTask.OcrWithRegion.ToWireValue());
        Assert.Equal("referring_expression_segmentation", WorkerTask.ReferringExpressionSegmentation.ToWireValue());
    }

    [Fact]
    public void Every_task_has_a_wire_value()
    {
        foreach (WorkerTask task in Enum.GetValues<WorkerTask>())
            Assert.False(string.IsNullOrWhiteSpace(task.ToWireValue()));
    }
}
