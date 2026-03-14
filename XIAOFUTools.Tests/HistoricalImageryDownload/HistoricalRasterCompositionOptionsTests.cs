using XIAOFUTools.Tools.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class HistoricalRasterCompositionOptionsTests
{
    [Fact]
    public void OutputRaster_UsesRgbaBands()
    {
        Assert.Equal(4, HistoricalRasterCompositionOptions.OutputBandCount);
        Assert.True(HistoricalRasterCompositionOptions.UseAlphaBand);
    }

    [Fact]
    public void OutputRaster_DoesNotApplyRgbNoData()
    {
        Assert.False(HistoricalRasterCompositionOptions.ApplyRgbNoData);
    }
}
