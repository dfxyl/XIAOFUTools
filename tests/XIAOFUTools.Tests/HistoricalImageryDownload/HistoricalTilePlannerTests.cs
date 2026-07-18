using XIAOFUTools.Features.General.HistoricalImageryDownload.Core;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class HistoricalTilePlannerTests
{
    [Fact]
    public void PlanGoogle_ReturnsSingleTileWithExpectedPathAndOffsets()
    {
        var plan = HistoricalTilePlanner.PlanGoogle(0, 0, 10, 10, 1);

        var tile = Assert.Single(plan.Tiles);
        Assert.Equal(1, tile.Row);
        Assert.Equal(1, tile.Column);
        Assert.Equal("02", tile.Path);
        Assert.Equal(0, tile.PixelOffsetX);
        Assert.Equal(0, tile.PixelOffsetY);
        Assert.Equal(256, plan.PixelWidth);
        Assert.Equal(256, plan.PixelHeight);
    }

    [Fact]
    public void PlanWebMercator_ReturnsSingleTileWithExpectedOffsets()
    {
        var plan = HistoricalTilePlanner.PlanWebMercator(1, 1, 1000, 1000, 1);

        var tile = Assert.Single(plan.Tiles);
        Assert.Equal(0, tile.Row);
        Assert.Equal(1, tile.Column);
        Assert.Equal(0, tile.PixelOffsetX);
        Assert.Equal(0, tile.PixelOffsetY);
        Assert.Equal(256, plan.PixelWidth);
        Assert.Equal(256, plan.PixelHeight);
    }
}
